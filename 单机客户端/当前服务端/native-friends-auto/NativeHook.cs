using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace FlightIslandNative {
public sealed class PanelHook : IDisposable {
 IntPtr module, hook; readonly IntPtr window;
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibraryW(string path);
 [DllImport("kernel32.dll", CharSet=CharSet.Ansi, ExactSpelling=true, SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
 [DllImport("user32.dll", SetLastError=true)] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SetWindowsHookExW(int id,IntPtr proc,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SendMessageTimeoutW(IntPtr window,uint message,UIntPtr wParam,IntPtr lParam,uint flags,uint timeout,out UIntPtr result);
 public PanelHook(string path,IntPtr hwnd,int pid) {
  if(IntPtr.Size!=4)throw new InvalidOperationException("Please use the 32-bit launcher.");
  window=hwnd; uint actual; uint thread=GetWindowThreadProcessId(window,out actual);
  if(thread==0||actual!=(uint)pid)throw new InvalidOperationException("Game window changed. Reopen the connector.");
  try {module=LoadLibraryW(path);if(module==IntPtr.Zero)throw new Win32Exception();
   IntPtr proc=GetProcAddress(module,"Flight_Hook");if(proc==IntPtr.Zero)throw new InvalidOperationException("Install the v3 DLL first.");
   hook=SetWindowsHookExW(4,proc,module,thread);if(hook==IntPtr.Zero)throw new Win32Exception();
  } catch {Dispose();throw;}
 }
 public void Initialize(){UIntPtr result;if(SendMessageTimeoutW(window,0x87f1,new UIntPtr(0x51464632),IntPtr.Zero,2,8000,out result)==IntPtr.Zero)throw new Win32Exception();}
 public void Dispose(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}if(module!=IntPtr.Zero){FreeLibrary(module);module=IntPtr.Zero;}}
}
}
