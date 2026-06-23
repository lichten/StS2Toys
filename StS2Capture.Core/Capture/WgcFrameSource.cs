using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace StS2Capture.Capture;

/// <summary>
/// Windows.Graphics.Capture（WGC）によるキャプチャ（主）。
/// Godot（Vulkan/DX）描画や全画面でも黒画面になりにくい。
/// D3D サーフェスからのピクセル読み出しは <c>SoftwareBitmap.CreateCopyFromSurfaceAsync</c>
/// を使い、ID3D11DeviceContext の手書き interop を避ける。
/// 同一 hwnd に対してはセッションを保持し、FrameArrived で最新フレームを更新する。
/// </summary>
public sealed class WgcFrameSource : IFrameSource
{
    public string Name => "WGC (Windows.Graphics.Capture)";

    IntPtr _hwnd;
    IDirect3DDevice? _device;
    Direct3D11CaptureFramePool? _framePool;
    GraphicsCaptureSession? _session;
    GraphicsCaptureItem? _item;

    readonly object _lock = new();
    Bitmap? _latest;

    public Bitmap? CaptureFrame(IntPtr hwnd)
    {
        EnsureSession(hwnd);

        // 最新フレームが来るまで短時間待つ（初回 / 画面更新待ち）。
        for (int i = 0; i < 40; i++)
        {
            lock (_lock)
            {
                if (_latest is not null)
                    return new Bitmap(_latest); // 独立した所有権を返す
            }
            Thread.Sleep(25);
        }
        lock (_lock)
            return _latest is not null ? new Bitmap(_latest) : null;
    }

    void EnsureSession(IntPtr hwnd)
    {
        if (_session is not null && _hwnd == hwnd) return;
        ResetSession();

        _hwnd = hwnd;
        _device = CreateDirect3DDevice();
        _item = CreateItemForWindow(hwnd);

        var size = _item.Size;
        // CreateFreeThreaded: DispatcherQueue 不要のバックグラウンドスレッドで
        // FrameArrived を発火させる（WinForms スレッドには DispatcherQueue が無いため必須）。
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
        _framePool.FrameArrived += OnFrameArrived;

        _session = _framePool.CreateCaptureSession(_item);
        _session.StartCapture();
    }

    void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null) return;

            // D3D サーフェス → SoftwareBitmap（BGRA8）
            var sb = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface).AsTask().GetAwaiter().GetResult();
            var bmp = SoftwareBitmapToBitmap(sb);
            sb.Dispose();

            lock (_lock)
            {
                _latest?.Dispose();
                _latest = bmp;
            }
        }
        catch { /* フレーム取得の一時的失敗は無視 */ }
    }

    static Bitmap SoftwareBitmapToBitmap(SoftwareBitmap sb)
    {
        // BGRA8 前提。CopyToBuffer でバイト列を取り出して System.Drawing.Bitmap を組む。
        int w = sb.PixelWidth, h = sb.PixelHeight;
        var buffer = new Windows.Storage.Streams.Buffer((uint)(w * h * 4));
        sb.CopyToBuffer(buffer);

        var bytes = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer))
            reader.ReadBytes(bytes);

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            // ストライドが一致する前提（32bpp なので w*4）。差異があれば行単位コピー。
            int srcStride = w * 4;
            if (data.Stride == srcStride)
            {
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            else
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(bytes, y * srcStride, data.Scan0 + y * data.Stride, srcStride);
            }
        }
        finally { bmp.UnlockBits(data); }
        return bmp;
    }

    // ---- WinRT / D3D interop ----

    static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    [DllImport("d3d11.dll", SetLastError = true)]
    static extern int D3D11CreateDevice(
        IntPtr pAdapter, int driverType, IntPtr software, uint flags,
        IntPtr pFeatureLevels, uint featureLevels, uint sdkVersion,
        out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll")]
    static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    static IDirect3DDevice CreateDirect3DDevice()
    {
        const int D3D_DRIVER_TYPE_HARDWARE = 1;
        const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
        const uint D3D11_SDK_VERSION = 7;

        int hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT, IntPtr.Zero, 0, D3D11_SDK_VERSION,
            out var devicePtr, out _, out var contextPtr);
        if (hr < 0 || devicePtr == IntPtr.Zero)
            throw new InvalidOperationException($"D3D11CreateDevice failed (0x{hr:X8})");

        try
        {
            var iid = IID_IDXGIDevice;
            int qhr = Marshal.QueryInterface(devicePtr, in iid, out var dxgiDevicePtr);
            if (qhr < 0 || dxgiDevicePtr == IntPtr.Zero)
                throw new InvalidOperationException($"QI IDXGIDevice failed (0x{qhr:X8})");

            try
            {
                int chr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out var inspectable);
                if (chr < 0 || inspectable == IntPtr.Zero)
                    throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice failed (0x{chr:X8})");

                try
                {
                    return MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
                }
                finally { Marshal.Release(inspectable); }
            }
            finally { Marshal.Release(dxgiDevicePtr); }
        }
        finally
        {
            if (contextPtr != IntPtr.Zero) Marshal.Release(contextPtr);
            Marshal.Release(devicePtr);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(IntPtr window, ref Guid riid, out IntPtr result);
        [PreserveSig] int CreateForMonitor(IntPtr monitor, ref Guid riid, out IntPtr result);
    }

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);

    static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        // GraphicsCaptureItem のアクティベーションファクトリから interop を取得する
        // （CsWinRT 版差に依存しないよう RoGetActivationFactory を直接叩く）。
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        int chr = WindowsCreateString(className, className.Length, out var hClass);
        if (chr < 0) throw new InvalidOperationException($"WindowsCreateString failed (0x{chr:X8})");
        try
        {
            var interopGuid = IID_IGraphicsCaptureItemInterop;
            int rhr = RoGetActivationFactory(hClass, ref interopGuid, out var factoryPtr);
            if (rhr < 0 || factoryPtr == IntPtr.Zero)
                throw new InvalidOperationException($"RoGetActivationFactory failed (0x{rhr:X8})");
            try
            {
                var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
                var itemGuid = IID_IGraphicsCaptureItem;
                int hr = interop.CreateForWindow(hwnd, ref itemGuid, out var itemPtr);
                if (hr < 0 || itemPtr == IntPtr.Zero)
                    throw new InvalidOperationException($"CreateForWindow failed (0x{hr:X8})");

                try { return GraphicsCaptureItem.FromAbi(itemPtr); }
                finally { Marshal.Release(itemPtr); }
            }
            finally { Marshal.Release(factoryPtr); }
        }
        finally { WindowsDeleteString(hClass); }
    }

    void ResetSession()
    {
        try { if (_framePool is not null) _framePool.FrameArrived -= OnFrameArrived; } catch { }
        try { _session?.Dispose(); } catch { }
        try { _framePool?.Dispose(); } catch { }
        _session = null;
        _framePool = null;
        _item = null;
        _device = null;
        lock (_lock) { _latest?.Dispose(); _latest = null; }
    }

    public void Dispose() => ResetSession();
}
