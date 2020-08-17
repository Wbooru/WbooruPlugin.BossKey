//code copy and modify from 
//https://github.com/justcoding121/Windows-User-Action-Hook
//within MIT License

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using static EventHook.Hooks.WindowHelper;

/// <summary>
/// //adapted from
/// https://gist.github.com/Ciantic/471698
/// </summary>
namespace EventHook.Hooks
{
    internal class SyncFactory : IDisposable
    {
        private readonly Lazy<MessageHandler> messageHandler;

        private readonly Lazy<TaskScheduler> scheduler;
        private bool hasUIThread;

        internal SyncFactory()
        {
            scheduler = new Lazy<TaskScheduler>(() =>
            {
                //if the calling thread is a UI thread then return its synchronization context
                //no need to create a message pump
                var dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
                if (dispatcher != null)
                {
                    if (SynchronizationContext.Current != null)
                    {
                        hasUIThread = true;
                        return TaskScheduler.FromCurrentSynchronizationContext();
                    }
                }

                TaskScheduler current = null;

                //if current task scheduler is null, create a message pump 
                //http://stackoverflow.com/questions/2443867/message-pump-in-net-windows-service
                //use async for performance gain!
                new Task(() =>
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            Volatile.Write(ref current, TaskScheduler.FromCurrentSynchronizationContext());
                        }), DispatcherPriority.Normal);
                    Dispatcher.Run();
                }).Start();

                //we called dispatcher begin invoke to get the Message Pump Sync Context
                //we check every 10ms until synchronization context is copied
                while (Volatile.Read(ref current) == null)
                {
                    Thread.Sleep(10);
                }

                return Volatile.Read(ref current);
            });

            messageHandler = new Lazy<MessageHandler>(() =>
            {
                MessageHandler msgHandler = null;
                //get the mesage handler dummy window created using the UI sync context
                new Task(e => { Volatile.Write(ref msgHandler, new MessageHandler()); }, GetTaskScheduler()).Start();

                //wait here until the window is created on UI thread
                while (Volatile.Read(ref msgHandler) == null)
                {
                    Thread.Sleep(10);
                }

                ;

                return Volatile.Read(ref msgHandler);
            });

            Initialize();
        }

        public void Dispose()
        {
            if (messageHandler?.Value != null)
            {
                messageHandler.Value.DestroyHandle();
            }
        }

        /// <summary>
        ///     Initialize the required message pump for all the hooks
        /// </summary>
        private void Initialize()
        {
            GetTaskScheduler();
            GetHandle();
        }

        /// <summary>
        ///     Get the UI task scheduler
        /// </summary>
        /// <returns></returns>
        internal TaskScheduler GetTaskScheduler()
        {
            return scheduler.Value;
        }

        /// <summary>
        ///     Get the handle of the window we created on the UI thread
        /// </summary>
        /// <returns></returns>
        internal IntPtr GetHandle()
        {
            var handle = IntPtr.Zero;

            if (hasUIThread)
            {
                try
                {
                    handle = Process.GetCurrentProcess().MainWindowHandle;

                    if (handle != IntPtr.Zero)
                    {
                        return handle;
                    }
                }
                catch
                {
                }
            }

            return messageHandler.Value.Handle;
        }
    }

    /// <summary>
    ///     A dummy class to create a dummy invisible window object
    /// </summary>
    internal class MessageHandler : NativeWindow
    {
        internal MessageHandler()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message msg)
        {
            base.WndProc(ref msg);
        }
    }

    internal enum ShellEvents
    {
        HSHELL_WINDOWCREATED = 1,
        HSHELL_WINDOWDESTROYED = 2,
        HSHELL_ACTIVATESHELLWINDOW = 3,
        HSHELL_WINDOWACTIVATED = 4,
        HSHELL_GETMINRECT = 5,
        HSHELL_REDRAW = 6,
        HSHELL_TASKMAN = 7,
        HSHELL_LANGUAGE = 8,
        HSHELL_SYSMENU = 9,
        HSHELL_ENDTASK = 10,
        HSHELL_ACCESSIBILITYSTATE = 11,
        HSHELL_APPCOMMAND = 12,
        HSHELL_WINDOWREPLACED = 13,
        HSHELL_WINDOWREPLACING = 14,
        HSHELL_HIGHBIT = 0x8000,
        HSHELL_FLASH = HSHELL_REDRAW | HSHELL_HIGHBIT,
        HSHELL_RUDEAPPACTIVATED = HSHELL_WINDOWACTIVATED | HSHELL_HIGHBIT
    }

    internal enum WindowStyle
    {
        WS_OVERLAPPED = 0x00000000,
        WS_POPUP = -2147483648,
        WS_CHILD = 0x40000000,
        WS_MINIMIZE = 0x20000000,
        WS_VISIBLE = 0x10000000,
        WS_DISABLED = 0x08000000,
        WS_CLIPSIBLINGS = 0x04000000,
        WS_CLIPCHILDREN = 0x02000000,
        WS_MAXIMIZE = 0x01000000,
        WS_CAPTION = 0x00C00000,
        WS_BORDER = 0x00800000,
        WS_DLGFRAME = 0x00400000,
        WS_VSCROLL = 0x00200000,
        WS_HSCROLL = 0x00100000,
        WS_SYSMENU = 0x00080000,
        WS_THICKFRAME = 0x00040000,
        WS_GROUP = 0x00020000,
        WS_TABSTOP = 0x00010000,
        WS_MINIMIZEBOX = 0x00020000,
        WS_MAXIMIZEBOX = 0x00010000,
        WS_TILED = WS_OVERLAPPED,
        WS_ICONIC = WS_MINIMIZE,
        WS_SIZEBOX = WS_THICKFRAME,
        WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,

        WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU |
                              WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
        WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
        WS_CHILDWINDOW = WS_CHILD
    }

    internal enum WindowStyleEx
    {
        WS_EX_DLGMODALFRAME = 0x00000001,
        WS_EX_NOPARENTNOTIFY = 0x00000004,
        WS_EX_TOPMOST = 0x00000008,
        WS_EX_ACCEPTFILES = 0x00000010,
        WS_EX_TRANSPARENT = 0x00000020,
        WS_EX_MDICHILD = 0x00000040,
        WS_EX_TOOLWINDOW = 0x00000080,
        WS_EX_WINDOWEDGE = 0x00000100,
        WS_EX_CLIENTEDGE = 0x00000200,
        WS_EX_CONTEXTHELP = 0x00000400,
        WS_EX_RIGHT = 0x00001000,
        WS_EX_LEFT = 0x00000000,
        WS_EX_RTLREADING = 0x00002000,
        WS_EX_LTRREADING = 0x00000000,
        WS_EX_LEFTSCROLLBAR = 0x00004000,
        WS_EX_RIGHTSCROLLBAR = 0x00000000,
        WS_EX_CONTROLPARENT = 0x00010000,
        WS_EX_STATICEDGE = 0x00020000,
        WS_EX_APPWINDOW = 0x00040000,
        WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
        WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
        WS_EX_LAYERED = 0x00080000,
        WS_EX_NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
        WS_EX_LAYOUTRTL = 0x00400000, // Right to left mirroring
        WS_EX_COMPOSITED = 0x02000000,
        WS_EX_NOACTIVATE = 0x08000000
    }

    internal enum GWLIndex
    {
        GWL_WNDPROC = -4,
        GWL_HINSTANCE = -6,
        GWL_HWNDPARENT = -8,
        GWL_STYLE = -16,
        GWL_EXSTYLE = -20,
        GWL_USERDATA = -21,
        GWL_ID = -12
    }

    internal enum GetWindowContstants
    {
        GW_HWNDFIRST = 0,
        GW_HWNDLAST = 1,
        GW_HWNDNEXT = 2,
        GW_HWNDPREV = 3,
        GW_OWNER = 4,
        GW_CHILD = 5,

        GW_ENABLEDPOPUP = 6,
        GW_MAX = 6
    }

    internal class User32
    {
        [DllImport("user32.dll")]
        internal static extern int GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowText(IntPtr hWnd, [Out] StringBuilder lpString, int nMaxCount);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SetClipboardViewer(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool ChangeClipboardChain(
            IntPtr hWndRemove, // handle to window to remove
            IntPtr hWndNewNext // handle to next window
        );

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool EnumWindows(EnumWindowsProc numFunc, IntPtr lParam);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr GetWindow(IntPtr hwnd, int uCmd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool RegisterShellHook(IntPtr hWnd, int flags);

        /// <summary>
        ///     Registers a specified Shell window to receive certain messages for events or notifications that are useful to
        ///     Shell applications. The event messages received are only those sent to the Shell window associated with the
        ///     specified window's desktop. Many of the messages are the same as those that can be received after calling
        ///     the SetWindowsHookEx function and specifying WH_SHELL for the hook type. The difference with
        ///     RegisterShellHookWindow is that the messages are received through the specified window's WindowProc
        ///     and not through a call back procedure.
        /// </summary>
        /// <param name="hWnd">[in] Handle to the window to register for Shell hook messages.</param>
        /// <returns>TRUE if the function succeeds; FALSE if the function fails. </returns>
        /// <remarks>
        ///     As with normal window messages, the second parameter of the window procedure identifies the
        ///     message as a "WM_SHELLHOOKMESSAGE". However, for these Shell hook messages, the
        ///     message value is not a pre-defined constant like other message identifiers (IDs) such as
        ///     WM_COMMAND. The value must be obtained dynamically using a call to
        ///     RegisterWindowMessage(TEXT("SHELLHOOK"));. This precludes handling these messages using
        ///     a traditional switch statement which requires ID values that are known at compile time.
        ///     For handling Shell hook messages, the normal practice is to code an If statement in the default
        ///     section of your switch statement and then handle the message if the value of the message ID
        ///     is the same as the value obtained from the RegisterWindowMessage call.
        ///     for more see MSDN
        /// </remarks>
        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern bool RegisterShellHookWindow(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        internal static extern uint RegisterWindowMessage(string Message);

        [DllImport("user32.dll")]
        internal static extern void SetTaskmanWindow(IntPtr hwnd);
    }

    /// <summary>
    ///     A helper class to get window names/handles etc
    /// </summary>
    internal class WindowHelper
    {
        /// <summary>
        ///     Get the handle of current acitive window on screen if any
        /// </summary>
        /// <returns></returns>
        internal static IntPtr GetActiveWindowHandle()
        {
            try
            {
                return (IntPtr)User32.GetForegroundWindow();
            }
            catch (Exception)
            {
                // ignored
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The the application exe path of this window
        /// </summary>
        /// <param name="hWnd">window handle</param>
        /// <returns></returns>
        internal static string GetAppPath(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                uint pid;
                User32.GetWindowThreadProcessId(hWnd, out pid);
                var proc = Process.GetProcessById((int)pid);
                return proc.MainModule.FileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Get the title text of this window
        /// </summary>
        /// <param name="hWnd">widow handle</param>
        /// <returns></returns>
        internal static string GetWindowText(IntPtr hWnd)
        {
            try
            {
                int length = User32.GetWindowTextLength(hWnd);
                var sb = new StringBuilder(length + 1);
                User32.GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        ///     A concurrent queue facilitating async dequeue with minimal locking
        ///     Assumes single/multi-threaded producer and a single-threaded consumer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal class AsyncConcurrentQueue<T>
        {
            /// <summary>
            ///     Backing queue
            /// </summary>
            private readonly ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

            /// <summary>
            ///     Wake up any pending dequeue task
            /// </summary>
            private TaskCompletionSource<bool> dequeueTask;
            private SemaphoreSlim @dequeueTaskLock = new SemaphoreSlim(1);
            private CancellationToken taskCancellationToken;

            internal AsyncConcurrentQueue(CancellationToken taskCancellationToken)
            {
                this.taskCancellationToken = taskCancellationToken;
            }

            /// <summary>
            ///     Supports multi-threaded producers
            /// </summary>
            /// <param name="value"></param>
            internal void Enqueue(T value)
            {
                queue.Enqueue(value);

                //signal 
                dequeueTaskLock.Wait();
                dequeueTask?.TrySetResult(true);
                dequeueTaskLock.Release();

            }

            /// <summary>
            ///     Assumes a single-threaded consumer!
            /// </summary>
            /// <returns></returns>
            internal async Task<T> DequeueAsync()
            {
                T result;
                queue.TryDequeue(out result);

                if (result != null)
                {
                    return result;
                }

                await dequeueTaskLock.WaitAsync();
                dequeueTask = new TaskCompletionSource<bool>();
                dequeueTaskLock.Release();

                taskCancellationToken.Register(() => dequeueTask.TrySetCanceled());
                await dequeueTask.Task;

                queue.TryDequeue(out result);
                return result;
            }
        }

        /// <summary>
        ///     Get the application description file attribute from path of an executable file
        /// </summary>
        /// <param name="appPath"></param>
        /// <returns></returns>
        internal static string GetAppDescription(string appPath)
        {
            if (appPath == null)
            {
                return null;
            }

            try
            {
                return FileVersionInfo.GetVersionInfo(appPath).FileDescription;
            }
            catch
            {
                return null;
            }
        }
    }
    internal class KeyboardHook
    {
        private KeyboardListener _listener;
        internal event RawKeyEventHandler KeyDown = delegate { };
        internal event RawKeyEventHandler KeyUp = delegate { };

        internal void Start()
        {
            _listener = new KeyboardListener();
            _listener.KeyDown += KListener_KeyDown;
            _listener.KeyUp += KListener_KeyUp;
        }

        internal void Stop()
        {
            if (_listener != null)
            {
                _listener.KeyDown -= KListener_KeyDown;
                _listener.KeyUp -= KListener_KeyUp;
                _listener.Dispose();
            }
        }


        private void KListener_KeyDown(object sender, RawKeyEventArgs args)
        {
            KeyDown(sender, args);
        }

        private void KListener_KeyUp(object sender, RawKeyEventArgs args)
        {
            KeyUp(sender, args);
        }
    }

    internal class KeyboardListener : IDisposable
    {
        private readonly Dispatcher _dispatcher;

        //http://stackoverflow.com/questions/6193711/call-has-been-made-on-garbage-collected-delegate-in-c
        private readonly InterceptKeys.LowLevelKeyboardProc _hookProcDelegateToAvoidGC;

        /// <summary>
        ///     Creates global keyboard listener.
        /// </summary>
        internal KeyboardListener()
        {
            // Dispatcher thread handling the KeyDown/KeyUp events.
            _dispatcher = Dispatcher.CurrentDispatcher;

            // We have to store the LowLevelKeyboardProc, so that it is not garbage collected runtime
            _hookProcDelegateToAvoidGC = LowLevelKeyboardProc;
            // Set the hook
            _hookId = InterceptKeys.SetHook(_hookProcDelegateToAvoidGC);

            // Assign the asynchronous callback event
            _hookedKeyboardCallbackAsync = KeyboardListener_KeyboardCallbackAsync;
        }

        #region IDisposable Members

        /// <summary>
        ///     Disposes the hook.
        ///     <remarks>This call is required as it calls the UnhookWindowsHookEx.</remarks>
        /// </summary>
        public void Dispose()
        {
            InterceptKeys.UnhookWindowsHookEx(_hookId);
        }

        #endregion

        /// <summary>
        ///     Destroys global keyboard listener.
        /// </summary>
        ~KeyboardListener()
        {
            Dispose();
        }

        /// <summary>
        ///     Fired when any of the keys is pressed down.
        /// </summary>
        internal event RawKeyEventHandler KeyDown;

        /// <summary>
        ///     Fired when any of the keys is released.
        /// </summary>
        internal event RawKeyEventHandler KeyUp;

        #region Inner workings

        /// <summary>
        ///     Hook ID
        /// </summary>
        private readonly IntPtr _hookId;

        /// <summary>
        ///     Asynchronous callback hook.
        /// </summary>
        /// <param name="character">Character</param>
        /// <param name="keyEvent">Keyboard event</param>
        /// <param name="vkCode">VKCode</param>
        private delegate void KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode, string character);

        /// <summary>
        ///     Actual callback hook.
        ///     <remarks>Calls asynchronously the asyncCallback.</remarks>
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
                    wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYUP ||
                    wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYDOWN ||
                    wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYUP)
                {
                    // Captures the character(s) pressed only on WM_KEYDOWN
                    string chars = InterceptKeys.VkCodeToString((uint)Marshal.ReadInt32(lParam),
                        wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_KEYDOWN ||
                        wParam.ToUInt32() == (int)InterceptKeys.KeyEvent.WM_SYSKEYDOWN);

                    Task.Run(() => _hookedKeyboardCallbackAsync?.Invoke((InterceptKeys.KeyEvent)wParam.ToUInt32(), Marshal.ReadInt32(lParam), chars));
                }
            }

            return InterceptKeys.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        ///     Event to be invoked asynchronously (BeginInvoke) each time key is pressed.
        /// </summary>
        private readonly KeyboardCallbackAsync _hookedKeyboardCallbackAsync;

        /// <summary>
        ///     HookCallbackAsync procedure that calls accordingly the KeyDown or KeyUp events.
        /// </summary>
        /// <param name="keyEvent">Keyboard event</param>
        /// <param name="vkCode">VKCode</param>
        /// <param name="character">Character as string.</param>
        private void KeyboardListener_KeyboardCallbackAsync(InterceptKeys.KeyEvent keyEvent, int vkCode,
            string character)
        {
            switch (keyEvent)
            {
                // KeyDown events
                case InterceptKeys.KeyEvent.WM_KEYDOWN:
                    if (KeyDown != null)
                    {
                        _dispatcher.BeginInvoke(new RawKeyEventHandler(KeyDown), this,
                            new RawKeyEventArgs(vkCode, false, character, 0));
                    }

                    break;
                case InterceptKeys.KeyEvent.WM_SYSKEYDOWN:
                    if (KeyDown != null)
                    {
                        _dispatcher.BeginInvoke(new RawKeyEventHandler(KeyDown), this,
                            new RawKeyEventArgs(vkCode, true, character, 0));
                    }

                    break;

                // KeyUp events
                case InterceptKeys.KeyEvent.WM_KEYUP:
                    if (KeyUp != null)
                    {
                        _dispatcher.BeginInvoke(new RawKeyEventHandler(KeyUp), this,
                            new RawKeyEventArgs(vkCode, false, character, 1));
                    }

                    break;
                case InterceptKeys.KeyEvent.WM_SYSKEYUP:
                    if (KeyUp != null)
                    {
                        _dispatcher.BeginInvoke(new RawKeyEventHandler(KeyUp), this,
                            new RawKeyEventArgs(vkCode, true, character, 1));
                    }

                    break;
            }
        }

        #endregion
    }

    /// <summary>
    ///     Raw KeyEvent arguments.
    /// </summary>
    internal class RawKeyEventArgs : EventArgs
    {
        /// <summary>
        ///     Unicode character of key pressed.
        /// </summary>
        internal string Character;

        /// <summary>
        ///     Up(1) or Down(0)
        /// </summary>
        internal int EventType;

        /// <summary>
        ///     Is the hitted key system key.
        /// </summary>
        internal bool IsSysKey;

        /// <summary>
        ///     WPF Key of the key.
        /// </summary>
        internal Key Key;

        /// <summary>
        ///     VKCode of the key.
        /// </summary>
        internal int VkCode;


        /// <summary>
        ///     Create raw keyevent arguments.
        /// </summary>
        /// <param name="vkCode"></param>
        /// <param name="isSysKey"></param>
        /// <param name="character">Character</param>
        /// <param name="type"></param>
        internal RawKeyEventArgs(int vkCode, bool isSysKey, string character, int type)
        {
            VkCode = vkCode;
            IsSysKey = isSysKey;
            Character = character;
            Key = KeyInterop.KeyFromVirtualKey(vkCode);
            EventType = type;
        }

        /// <summary>
        ///     Convert to string.
        /// </summary>
        /// <returns>Returns string representation of this key, if not possible empty string is returned.</returns>
        public override string ToString()
        {
            return Character;
        }
    }

    /// <summary>
    ///     Raw keyevent handler.
    /// </summary>
    /// <param name="sender">sender</param>
    /// <param name="args">raw keyevent arguments</param>
    internal delegate void RawKeyEventHandler(object sender, RawKeyEventArgs args);

    #region WINAPI Helper class

    /// <summary>
    ///     Winapi Key interception helper class.
    /// </summary>
    internal static class InterceptKeys
    {
        internal static int WH_KEYBOARD_LL = 13;

        internal static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod,
            uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, UIntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, UIntPtr wParam, IntPtr lParam);

        /// <summary>
        ///     Key event
        /// </summary>
        internal enum KeyEvent
        {
            /// <summary>
            ///     Key down
            /// </summary>
            WM_KEYDOWN = 256,

            /// <summary>
            ///     Key up
            /// </summary>
            WM_KEYUP = 257,

            /// <summary>
            ///     System key up
            /// </summary>
            WM_SYSKEYUP = 261,

            /// <summary>
            ///     System key down
            /// </summary>
            WM_SYSKEYDOWN = 260
        }

        #region Convert VKCode to string

        // Note: Sometimes single VKCode represents multiple chars, thus string. 
        // E.g. typing "^1" (notice that when pressing 1 the both characters appear, 
        // because of this behavior, "^" is called dead key)

        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            [Out] [MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetKeyboardLayout(uint dwLayout);

        [DllImport("User32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("User32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private static uint _lastVkCode;
        private static uint _lastScanCode;
        private static byte[] _lastKeyState = new byte[255];
        private static bool _lastIsDead;

        /// <summary>
        ///     Convert VKCode to Unicode.
        ///     <remarks>isKeyDown is required for because of keyboard state inconsistencies!</remarks>
        /// </summary>
        /// <param name="vkCode">VKCode</param>
        /// <param name="isKeyDown">Is the key down event?</param>
        /// <returns>String representing single unicode character.</returns>
        internal static string VkCodeToString(uint vkCode, bool isKeyDown)
        {
            // ToUnicodeEx needs StringBuilder, it populates that during execution.
            var sbString = new StringBuilder(5);

            var bKeyState = new byte[255];
            bool bKeyStateStatus;
            bool isDead = false;

            // Gets the current windows window handle, threadID, processID
            var currentHWnd = GetForegroundWindow();
            uint currentProcessId;
            uint currentWindowThreadId = GetWindowThreadProcessId(currentHWnd, out currentProcessId);

            // This programs Thread ID
            uint thisProgramThreadId = GetCurrentThreadId();

            // Attach to active thread so we can get that keyboard state
            if (AttachThreadInput(thisProgramThreadId, currentWindowThreadId, true))
            {
                // Current state of the modifiers in keyboard
                bKeyStateStatus = GetKeyboardState(bKeyState);

                // Detach
                AttachThreadInput(thisProgramThreadId, currentWindowThreadId, false);
            }
            else
            {
                // Could not attach, perhaps it is this process?
                bKeyStateStatus = GetKeyboardState(bKeyState);
            }

            // On failure we return empty string.
            if (!bKeyStateStatus)
            {
                return "";
            }

            // Gets the layout of keyboard
            var hkl = GetKeyboardLayout(currentWindowThreadId);

            // Maps the virtual keycode
            uint lScanCode = MapVirtualKeyEx(vkCode, 0, hkl);

            // Keyboard state goes inconsistent if this is not in place. In other words, we need to call above commands in UP events also.
            if (!isKeyDown)
            {
                return "";
            }

            // Converts the VKCode to unicode
            int relevantKeyCountInBuffer =
                ToUnicodeEx(vkCode, lScanCode, bKeyState, sbString, sbString.Capacity, 0, hkl);

            string ret = string.Empty;

            switch (relevantKeyCountInBuffer)
            {
                // Dead keys (^,`...)
                case -1:
                    isDead = true;

                    // We must clear the buffer because ToUnicodeEx messed it up, see below.
                    ClearKeyboardBuffer(vkCode, lScanCode, hkl);
                    break;

                case 0:
                    break;

                // Single character in buffer
                case 1:
                    ret = sbString[0].ToString();
                    break;

                // Two or more (only two of them is relevant)
                default:
                    ret = sbString.ToString().Substring(0, 2);
                    break;
            }

            // We inject the last dead key back, since ToUnicodeEx removed it.
            // More about this peculiar behavior see e.g: 
            //   http://www.experts-exchange.com/Programming/System/Windows__Programming/Q_23453780.html
            //   http://blogs.msdn.com/michkap/archive/2005/01/19/355870.aspx
            //   http://blogs.msdn.com/michkap/archive/2007/10/27/5717859.aspx
            if (_lastVkCode != 0 && _lastIsDead)
            {
                var sbTemp = new StringBuilder(5);
                ToUnicodeEx(_lastVkCode, _lastScanCode, _lastKeyState, sbTemp, sbTemp.Capacity, 0, hkl);
                _lastVkCode = 0;

                return ret;
            }

            // Save these
            _lastScanCode = lScanCode;
            _lastVkCode = vkCode;
            _lastIsDead = isDead;
            _lastKeyState = (byte[])bKeyState.Clone();

            return ret;
        }

        private static void ClearKeyboardBuffer(uint vk, uint sc, IntPtr hkl)
        {
            var sb = new StringBuilder(10);

            int rc;
            do
            {
                var lpKeyStateNull = new byte[255];
                rc = ToUnicodeEx(vk, sc, lpKeyStateNull, sb, sb.Capacity, 0, hkl);
            } while (rc < 0);
        }

        #endregion
    }

    #endregion

    /// <summary>
    ///     Key press data.
    /// </summary>
    public class KeyInputEventArgs : EventArgs
    {
        public KeyData KeyData { get; set; }
    }

    /// <summary>
    ///     Key data.
    /// </summary>
    public class KeyData
    {
        public KeyEvent EventType;
        public string Keyname;
        public string UnicodeCharacter;
    }

    /// <summary>
    ///     Key press event type.
    /// </summary>
    public enum KeyEvent
    {
        down = 0,
        up = 1
    }

    /// <summary>
    ///     Wraps low level keyboard hook.
    ///     Uses a producer-consumer pattern to improve performance and to avoid operating system forcing unhook on delayed
    ///     user callbacks.
    /// </summary>
    public class KeyboardWatcher
    {
        private readonly object accesslock = new object();

        private readonly SyncFactory factory;

        private KeyboardHook keyboardHook;
        private AsyncConcurrentQueue<object> keyQueue;
        private CancellationTokenSource taskCancellationTokenSource;

        internal KeyboardWatcher(SyncFactory factory)
        {
            this.factory = factory;
        }

        private bool isRunning { get; set; }
        public event EventHandler<KeyInputEventArgs> OnKeyInput;

        /// <summary>
        ///     Start watching
        /// </summary>
        public void Start()
        {
            lock (accesslock)
            {
                if (!isRunning)
                {
                    taskCancellationTokenSource = new CancellationTokenSource();
                    keyQueue = new AsyncConcurrentQueue<object>(taskCancellationTokenSource.Token);

                    //This needs to run on UI thread context
                    //So use task factory with the shared UI message pump thread
                    Task.Factory.StartNew(() =>
                    {
                        keyboardHook = new KeyboardHook();
                        keyboardHook.KeyDown += KListener;
                        keyboardHook.KeyUp += KListener;
                        keyboardHook.Start();
                    },
                        CancellationToken.None,
                        TaskCreationOptions.None,
                        factory.GetTaskScheduler()).Wait();

                    Task.Factory.StartNew(() => ConsumeKeyAsync());

                    isRunning = true;
                }
            }
        }

        /// <summary>
        ///     Stop watching
        /// </summary>
        public void Stop()
        {
            lock (accesslock)
            {
                if (isRunning)
                {
                    if (keyboardHook != null)
                    {
                        //This needs to run on UI thread context
                        //So use task factory with the shared UI message pump thread
                        Task.Factory.StartNew(() =>
                        {
                            keyboardHook.KeyDown -= KListener;
                            keyboardHook.Stop();
                            keyboardHook = null;
                        },
                            CancellationToken.None,
                            TaskCreationOptions.None,
                            factory.GetTaskScheduler());
                    }

                    keyQueue.Enqueue(false);
                    isRunning = false;
                    taskCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        ///     Add key event to the producer queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KListener(object sender, RawKeyEventArgs e)
        {
            keyQueue.Enqueue(new KeyData
            {
                UnicodeCharacter = e.Character,
                Keyname = e.Key.ToString(),
                EventType = (KeyEvent)e.EventType
            });
        }

        /// <summary>
        ///     Consume events from the producer queue asynchronously
        /// </summary>
        /// <returns></returns>
        private async Task ConsumeKeyAsync()
        {
            while (isRunning)
            {
                //blocking here until a key is added to the queue
                var item = await keyQueue.DequeueAsync();
                if (item is bool)
                {
                    break;
                }

                KListener_KeyDown((KeyData)item);
            }
        }

        /// <summary>
        ///     Invoke user call backs
        /// </summary>
        /// <param name="kd"></param>
        private void KListener_KeyDown(KeyData kd)
        {
            OnKeyInput?.Invoke(null, new KeyInputEventArgs { KeyData = kd });
        }
    }
}