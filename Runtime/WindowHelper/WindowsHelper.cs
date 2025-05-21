using System;
using System.Runtime.InteropServices;
using UnityEngine;

/*
    아래와 같은 꼴로 사용 가능.
    WindowsHelper winHelper = GetComponent<WindowsHelper>();
        winHelper.SetFullScreen(true)
                    .SetSize(TargetDisplayWidth, TargetDisplayHeight)
                    .SetPosition(TargetDisplayPosition_X,TargetDisplayPosition_Y)
                    .SetTopMost(true)
                    .Apply();
*/
/// <summary>
/// TopMost / Window Position / Window Size 설정을 위한 Helper.
/// 에디터에서 사용할 경우 플레이 종료 시 이전 위치, 크기로 돌아가도록 함.
/// SetFullScreen(true) : 타이틀바를 제거.
/// </summary>
public class WindowsHelper : MonoBehaviour
{
    // 설정하려는 상태
    private WindowState _targetState = new WindowState();
    // 플레이 시작 전 원래 상태
    private WindowState _originalState = new WindowState();
    // Apply가 실행되었는지 여부
    private bool _isApplied = false;

    private Coroutine _applyCoroutine;
    private IntPtr windowHandle;

    private void OnApplicationQuit()
    {
#if UNITY_EDITOR
        if (_isApplied)
        {
            RestoreOriginalWindowState();
        }
#endif
    }

    private void SaveOriginalWindowState()
    {
        // 현재 창의 실제 상태를 저장
        _originalState = GetCurrentWindowState(windowHandle);
    }

    private WindowState GetCurrentWindowState(IntPtr windowHandle)
    {
        if (!WindowNative.IsWindow(windowHandle))
        {
            Debug.LogError("[WindowsHelper] Invalid window handle");
            return new WindowState();
        }

        var placement = new WindowNative.WINDOWPLACEMENT();
        /*  sizeof(WINDOWPLACEMENT) 바이트만큼 구조체 크기를 지정해야 Win32가 값을 채워 줌.
            MS 공식문서 요구사항.
        */
        placement.length = Marshal.SizeOf(typeof(WindowNative.WINDOWPLACEMENT));
        if (!WindowNative.GetWindowPlacement(windowHandle, out placement))
        {
            Debug.LogError("[WindowsHelper] Failed to get window placement");
            return new WindowState();
        }

        // 창이 최대화되어 있는지 확인
        bool isMaximized = (placement.showCmd == WindowNative.SW_MAXIMIZE);

        // 창의 실제 위치와 크기 가져오기
        WindowNative.RECT rect = placement.rcNormalPosition;

        // 현재 창의 실제 상태를 가져옴
        return new WindowState(
            rect.Left,
            rect.Top,
            rect.Width,
            rect.Height,
            false, // TopMost 상태는 별도로 확인 필요
            isMaximized
        );
    }

    private void RestoreOriginalWindowState()
    {
        // 타이틀 바 복원
        long style = WindowNative.GetWindowLong(windowHandle, WindowNative.GWL_STYLE);
        style |= WindowNative.WS_OVERLAPPEDWINDOW;
        style &= ~WindowNative.WS_POPUP;
        WindowNative.SetWindowLong(windowHandle, WindowNative.GWL_STYLE, (int)style);

        // 전체화면 해제
        //WindowNative.ShowWindow(windowHandle, WindowNative.SW_RESTORE);
        
        // 원래 상태로 복원
        WindowNative.SetWindowPos(windowHandle,
            WindowNative.HWND_NOTOPMOST,
            _originalState.x, _originalState.y,
            _originalState.width, _originalState.height,
            WindowNative.SWP_SHOWWINDOW);
    }

#region 설정 메서드들
    public WindowsHelper SetPosition(int x, int y)
    {
        _targetState.x = x;
        _targetState.y = y;
        _targetState.isPositionSet = true;
        return this;
    }

    public WindowsHelper SetSize(int width, int height)
    {
        _targetState.width = width;
        _targetState.height = height;
        _targetState.isSizeSet = true;
        return this;
    }

    public WindowsHelper SetTopMost(bool isTopMost)
    {
        _targetState.isTopMost = isTopMost;
        return this;
    }

    public WindowsHelper SetFullScreen(bool isFullScreen)
    {
        _targetState.isFullScreen = isFullScreen;
        return this;
    }
#endregion

    public void Apply()
    {
        windowHandle = WindowNative.GetActiveWindow();
        if (windowHandle == IntPtr.Zero)
        {
            Debug.LogError("[WindowsHelper] Failed to get window handle");
            return;
        }

        // 이전 코루틴이 있다면 중지
        if (_applyCoroutine != null)
            StopCoroutine(_applyCoroutine);

        SaveOriginalWindowState();
        
        // 즉시 실행
        ApplyWindowState();

        // 3초 후 다시 실행
        _applyCoroutine = StartCoroutine(ApplyAfterDelay());
    }

    private void ApplyWindowState()
    {
        if (_targetState.isFullScreen)
        {
            // 전체화면 모드일 때 타이틀 바 제거
            long style = WindowNative.GetWindowLong(windowHandle, WindowNative.GWL_STYLE);
            style &= ~WindowNative.WS_OVERLAPPEDWINDOW;
            style |= WindowNative.WS_POPUP;
            WindowNative.SetWindowLong(windowHandle, WindowNative.GWL_STYLE, (int)style);

            // 최대화
            //WindowNative.ShowWindow(windowHandle, WindowNative.SW_MAXIMIZE);
        }
        else
        {
            // 일반 모드일 때 타이틀 바 복원
            long style = WindowNative.GetWindowLong(windowHandle, WindowNative.GWL_STYLE);
            style |= WindowNative.WS_OVERLAPPEDWINDOW;
            style &= ~WindowNative.WS_POPUP;
            WindowNative.SetWindowLong(windowHandle, WindowNative.GWL_STYLE, (int)style);

            // 일반 모드로 복원
            //WindowNative.ShowWindow(windowHandle, WindowNative.SW_RESTORE);
        }

        // 설정되지 않은 값은 현재 상태 유지
        uint flags = WindowNative.SWP_SHOWWINDOW;
        if (!_targetState.isPositionSet)
            flags |= WindowNative.SWP_NOMOVE;
        if (!_targetState.isSizeSet)
            flags |= WindowNative.SWP_NOSIZE;

        WindowNative.SetWindowPos(windowHandle, 
            _targetState.isTopMost ? WindowNative.HWND_TOPMOST : WindowNative.HWND_NOTOPMOST,
            _targetState.x, _targetState.y,
            _targetState.width, _targetState.height,
            flags);

        // Apply 실행됨을 표시
        _isApplied = true;
    }

    private System.Collections.IEnumerator ApplyAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        ApplyWindowState();
    }

    private void OnDestroy()
    {
        // 코루틴 정리
        if (_applyCoroutine != null)
        {
            StopCoroutine(_applyCoroutine);
            _applyCoroutine = null;
        }
    }
}

#region WINAPI
// Windows API 관련 네이티브 메서드들을 모아둔 클래스
public static class WindowNative
{
    // TopMost 관련 상수
    public const int HWND_TOPMOST = -1;
    public const int HWND_NOTOPMOST = -2;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

    // 창 위치 및 크기 관련 상수
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;

    // 전체화면 관련 상수
    public const int SW_MAXIMIZE = 3;
    public const int SW_RESTORE = 9;

    // 윈도우 스타일 관련 상수
    public const int GWL_STYLE = -16;
    public const long WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const long WS_POPUP = 0x80000000;

    // RECT 구조체 정의
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    // WINDOWPLACEMENT 구조체 정의
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32", SetLastError = true)]
    public static extern IntPtr GetActiveWindow();

    [DllImport("user32", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32", SetLastError = true)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

    [DllImport("user32", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

// 창 상태를 저장하는 클래스
[Serializable]
public class WindowState
{
    public int x;
    public int y;
    public int width;
    public int height;
    public bool isTopMost;
    public bool isFullScreen;

    // 설정 여부를 추적하는 플래그
    public bool isPositionSet;
    public bool isSizeSet;

    public WindowState()
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;
        isTopMost = false;
        isFullScreen = false;
        isPositionSet = false;
        isSizeSet = false;
    }

    public WindowState(int x, int y, int width, int height, bool isTopMost, bool isFullScreen)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        this.isTopMost = isTopMost;
        this.isFullScreen = isFullScreen;
        this.isPositionSet = true;
        this.isSizeSet = true;
    }

    public WindowState Clone()
    {
        return new WindowState(x, y, width, height, isTopMost, isFullScreen)
        {
            isPositionSet = this.isPositionSet,
            isSizeSet = this.isSizeSet
        };
    }
}
#endregion