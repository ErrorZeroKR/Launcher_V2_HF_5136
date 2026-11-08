using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// Console.WriteLine 출력을 (1) 런처 하단 로그 창 (2) Logs\launcher_날짜시간.log 파일 에 동시에 남김.
    /// 처리되지 않은 예외도 기록. 별도 콘솔 창이 필요하면 실행 인자에 -console
    /// </summary>
    public static class AppLog
    {
        [DllImport("kernel32.dll")] private static extern bool AllocConsole();
        [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")] private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private static TeeWriter tee;
        private static TextWriter consoleWriter;

        private const int MaxPending = 20000;
        private static readonly ConcurrentQueue<string> uiQueue = new ConcurrentQueue<string>();
        private static int pending;
        private static StreamWriter file;
        private static bool initialized;

        public static string LogPath { get; private set; }
        public static string LogDirectory { get; private set; }

        /// <summary>로그 창이 아직 가져가지 않은 줄이 있는지</summary>
        public static bool HasPending { get { return !uiQueue.IsEmpty; } }

        /// <summary>로그 창이 한 줄씩 가져감 (시간 표시가 붙은 완성된 한 줄)</summary>
        public static bool TryDequeue(out string line)
        {
            if (uiQueue.TryDequeue(out line))
            {
                Interlocked.Decrement(ref pending);
                return true;
            }
            return false;
        }

        private static void EnqueueUi(string line)
        {
            uiQueue.Enqueue(line);
            if (Interlocked.Increment(ref pending) > MaxPending)
            {
                string drop;
                if (uiQueue.TryDequeue(out drop)) Interlocked.Decrement(ref pending);
            }
        }

        public static void Init(string[] args)
        {
            if (initialized) return;
            initialized = true;

            bool showConsole = LauncherSettings.Current.ShowConsole;
            if (args != null)
                foreach (string a in args)
                    if (a.Equals("-console", StringComparison.OrdinalIgnoreCase)) showConsole = true;

            try
            {
                string dir = Path.Combine(Environment.CurrentDirectory, "Logs");
                Directory.CreateDirectory(dir);
                LogDirectory = dir;
                LogPath = Path.Combine(dir, "launcher_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                file = new StreamWriter(new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(true)) { AutoFlush = true };
                PruneOldLogs();   // 오래된 로그는 설정한 개수만 남김
            }
            catch { file = null; }

            TextWriter con = null;
            if (showConsole)
            {
                try
                {
                    if (GetConsoleWindow() == IntPtr.Zero) AllocConsole();
                    IntPtr hwnd = GetConsoleWindow();
                    if (hwnd != IntPtr.Zero) DeleteMenu(GetSystemMenu(hwnd, false), 0xF060, 0); // 콘솔 X 버튼 비활성화(실수로 닫으면 런처도 종료되므로)
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Title = "KartRider Launcher 로그";
                    con = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                }
                catch { con = null; }
            }

            consoleWriter = con;
            tee = new TeeWriter(con, file);
            Console.SetOut(tee);
            Console.SetError(tee);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ex1 = e.ExceptionObject as Exception;
                Error("처리되지 않은 예외 (런처가 종료됩니다)", ex1 ?? new Exception(Convert.ToString(e.ExceptionObject)));
                try { MessageBox.Show("런처에서 치명적인 오류가 발생했습니다.\n\n" + (ex1 != null ? ex1.GetType().Name + ": " + ex1.Message : "") + "\n\n발생 위치:\n" + TopFrames(ex1, 4) + "\n\n로그 파일: " + LogPath, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            };
            TaskScheduler.UnobservedTaskException += (s, e) => { Error("Task 예외", e.Exception); e.SetObserved(); };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Console.WriteLine("[로그] 런처 종료");
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Error("UI 스레드 예외", e.Exception);
            }
            catch (Exception ex2) { Console.WriteLine("[로그] 예외 처리기 설정 실패: " + ex2.Message); }

            Console.WriteLine("[로그] 시작 {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Console.WriteLine("[로그] 로그 파일: {0}", LogPath ?? "(생성 실패)");
            Console.WriteLine("[로그] OS: {0}, 64비트 프로세스: {1}, 작업 폴더: {2}", Environment.OSVersion, Environment.Is64BitProcess, Environment.CurrentDirectory);
        }

        /// <summary>설정한 보관 개수보다 오래된 로그 파일을 지움 (지금 쓰는 로그는 항상 남김). 지운 개수를 돌려줌</summary>
        public static int PruneOldLogs()
        {
            int removed = 0;
            try
            {
                if (LogDirectory == null) return 0;
                int keep = Math.Max(1, LauncherSettings.Current.LogKeepCount);
                foreach (FileInfo old in new DirectoryInfo(LogDirectory).GetFiles("launcher_*.log").OrderByDescending(f => f.LastWriteTime).Skip(keep))
                {
                    if (LogPath != null && string.Equals(old.FullName, LogPath, StringComparison.OrdinalIgnoreCase)) continue;
                    try { old.Delete(); removed++; } catch { }
                }
            }
            catch { }
            return removed;
        }

        /// <summary>콘솔 창을 실행 중에 바로 보이거나 숨김 (처음 켤 때 콘솔 창을 만들고, 이후에는 숨기기/보이기만 함)</summary>
        public static void SetConsoleVisible(bool show)
        {
            try
            {
                if (show)
                {
                    if (GetConsoleWindow() == IntPtr.Zero) AllocConsole();
                    IntPtr hwnd = GetConsoleWindow();
                    if (hwnd != IntPtr.Zero)
                    {
                        DeleteMenu(GetSystemMenu(hwnd, false), 0xF060, 0);   // 콘솔 X 버튼 비활성화 (실수로 닫으면 런처도 종료되므로)
                        ShowWindow(hwnd, 5);                                  // SW_SHOW
                    }
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.Title = "KartRider Launcher 로그";
                    if (consoleWriter == null) consoleWriter = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
                    if (tee != null) tee.SetConsole(consoleWriter);
                }
                else
                {
                    if (tee != null) tee.SetConsole(null);
                    IntPtr hwnd = GetConsoleWindow();
                    if (hwnd != IntPtr.Zero) ShowWindow(hwnd, 0);             // SW_HIDE
                }
            }
            catch (Exception ex) { Console.WriteLine("[로그] 콘솔 창 표시 전환 실패: " + ex.Message); }
        }

        /// <summary>예외 스택의 앞쪽 몇 줄 (오류 창에 보여줘서 어디서 났는지 바로 알 수 있게)</summary>
        private static string TopFrames(Exception ex, int count)
        {
            try
            {
                if (ex == null || string.IsNullOrEmpty(ex.StackTrace)) return "(없음)";
                string[] lines = ex.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                for (int i = 0; i < lines.Length && i < count; i++)
                {
                    string l = lines[i].Trim();
                    int p = l.IndexOf(" in ", StringComparison.Ordinal);   // 파일 경로는 길어서 메서드 이름까지만
                    if (p > 0) l = l.Substring(0, p);
                    sb.AppendLine(l);
                }
                return sb.ToString().TrimEnd();
            }
            catch { return "(확인 불가)"; }
        }

        public static void Error(string where, Exception ex)
        {
            try { Console.WriteLine("[오류] " + where + Environment.NewLine + ex); } catch { }
        }

        private sealed class TeeWriter : TextWriter
        {
            private TextWriter con;
            private readonly TextWriter log;
            private readonly object sync = new object();
            private readonly StringBuilder lineBuf = new StringBuilder();
            private bool atLineStart = true;

            public TeeWriter(TextWriter con, TextWriter log) { this.con = con; this.log = log; }
            public void SetConsole(TextWriter w) { lock (sync) { con = w; } }
            public override Encoding Encoding { get { return Encoding.UTF8; } }
            public override void Write(char value) { Write(value.ToString()); }
            public override void Write(char[] buffer, int index, int count) { Write(new string(buffer, index, count)); }
            public override void WriteLine(string value) { Write((value ?? "") + Environment.NewLine); }
            public override void WriteLine() { Write(Environment.NewLine); }

            public override void Write(string value)
            {
                if (value == null) return;
                lock (sync)
                {
                    try { if (con != null) con.Write(value); } catch { }
                    try
                    {
                        var sb = new StringBuilder();
                        foreach (char c in value)
                        {
                            if (atLineStart && c != '\r' && c != '\n')
                            {
                                string ts = DateTime.Now.ToString("HH:mm:ss.fff ");
                                sb.Append(ts);
                                lineBuf.Append(ts);
                                atLineStart = false;
                            }
                            sb.Append(c);
                            if (c == '\n')
                            {
                                atLineStart = true;
                                EnqueueUi(lineBuf.ToString());
                                lineBuf.Clear();
                            }
                            else if (c != '\r')
                            {
                                lineBuf.Append(c);
                            }
                        }
                        if (log != null) log.Write(sb.ToString());
                    }
                    catch { }
                }
            }
        }
    }
}
