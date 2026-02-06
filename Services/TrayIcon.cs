using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SimpleStickyNotes.Services
{

    public class TrayIcon : IDisposable
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(
            IntPtr hInst,
            string lpszName,
            uint uType,
            int cxDesired,
            int cyDesired,
            uint fuLoad);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private IntPtr _iconHandle = IntPtr.Zero;

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x0010;
        private const uint LR_DEFAULTSIZE = 0x0040;

        private const int WM_USER = 0x0400;
        private const int WM_TRAYMESSAGE = WM_USER + 1;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        public event Action LeftClick;
        public event Action RightClick;

        private NotifyIconData _data;
        private HwndSource _source;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr iconId);

        private static readonly IntPtr IDI_APPLICATION = new IntPtr(0x7F00);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        public TrayIcon(string tooltip, string? iconPath = null)
        {
            CreateMessageWindow();

            IntPtr iconHandle = LoadDefaultIcon();

            IntPtr icon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);

            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                var hIcon = LoadImage(
                    IntPtr.Zero,
                    iconPath,
                    IMAGE_ICON,
                    0,
                    0,
                    LR_LOADFROMFILE | LR_DEFAULTSIZE);

                if (hIcon == IntPtr.Zero)
                {
                    uint err = GetLastError();
                    MessageBox.Show($"LoadImage failed.\nPath:\n{iconPath}\nError: {err}");
                }
                else
                {
                    icon = hIcon;
                    _iconHandle = hIcon;
                }
            }

            _data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NotifyIconData)),
                hWnd = _source.Handle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYMESSAGE,
                hIcon = icon,
                szTip = tooltip ?? "Tray Icon"
            };


            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                var hIcon = LoadImage(
                    IntPtr.Zero,
                    iconPath,
                    IMAGE_ICON,
                    0,
                    0,
                    LR_LOADFROMFILE | LR_DEFAULTSIZE);

                if (hIcon != IntPtr.Zero)
                {
                    icon = hIcon;
                    _iconHandle = hIcon;
                }
            }

            Shell_NotifyIcon(NIM_ADD, ref _data);
            Shell_NotifyIcon(NIM_MODIFY, ref _data);
        }

        private static IntPtr LoadDefaultIcon()
        {
            return LoadIcon(IntPtr.Zero, IDI_APPLICATION);
        }

        private void CreateMessageWindow()
        {
            var p = new HwndSourceParameters("TrayIconWnd")
            {
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP
                Width = 0,
                Height = 0
            };

            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == WM_TRAYMESSAGE)
            {
                int eventId = l.ToInt32();

                switch (eventId)
                {
                    case 0x0202: // WM_LBUTTONUP
                        LeftClick?.Invoke();
                        break;

                    case 0x0205: // WM_RBUTTONUP
                        RightClick?.Invoke();
                        break;
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Shell_NotifyIcon(NIM_DELETE, ref _data);

            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }

            _source?.Dispose();
        }
    }
}