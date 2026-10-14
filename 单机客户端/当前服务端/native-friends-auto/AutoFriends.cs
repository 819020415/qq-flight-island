using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;

namespace FlightIslandNative {
public static class AutoFriends {
 const string GameHash="23B45540390F11934398BAD4DBAF56315995963083E52DD6AD66BBE85002F039";
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] bytes,IntPtr size,out IntPtr count);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 static string Hash(string path){using(var f=File.OpenRead(path))using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(f)).Replace("-","");}
 static bool Read(IntPtr h,long address,byte[] data){IntPtr count;return ReadProcessMemory(h,new IntPtr(address),data,new IntPtr(data.Length),out count)&&count.ToInt64()==data.Length;}
 static string Role(IntPtr h,long address){var ptr=new byte[4];if(!Read(h,address+0x9869d4,ptr))return null;uint player=BitConverter.ToUInt32(ptr,0);if(player==0)return null;var name=new byte[16];if(!Read(h,(long)player+0x660,name))return null;int n=Array.IndexOf(name,(byte)0);if(n<1||n>15)return null;try {string s=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetString(name,0,n);foreach(char c in s)if(char.IsControl(c))return null;return s;}catch(DecoderFallbackException){return null;}}
 static void Log(string path,string text){try {File.AppendAllText(path,DateTime.Now.ToString("o")+" "+text+Environment.NewLine,Encoding.UTF8);}catch(IOException){}catch(UnauthorizedAccessException){}}
 public static void Run(int pid,string server,string account,string password,string gamePath,string folder){
  if(IntPtr.Size!=4)throw new InvalidOperationException("Requires a 32-bit helper.");
  bool created;using(var mutex=new Mutex(true,"Local\\QQFlightAutoFriends-"+pid,out created)){
   if(!created)return;
   string log=Path.Combine(folder,"auto-friends-"+pid+".log");
   using(var game=Process.GetProcessById(pid)){
    // Opening the process handle pins this identity; never follow a reused PID.
    IntPtr pinned=game.Handle;
    if(!string.Equals(Path.GetFullPath(game.MainModule.FileName),Path.GetFullPath(gamePath),StringComparison.OrdinalIgnoreCase)||Hash(gamePath)!=GameHash)throw new InvalidOperationException("Game executable mismatch.");
    string dll=Path.Combine(Path.GetDirectoryName(gamePath),"nmcogame.dll");
    if(Hash(dll)!=Hash(Path.Combine(folder,"nmcogame.dll")))throw new InvalidOperationException("Automatic friend DLL mismatch.");
    long baseAddress=game.MainModule.BaseAddress.ToInt64();IntPtr process=OpenProcess(0x410,false,pid);
    if(process==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
    Relay relay=null;PanelHook hook=null;string role=null;DateTime retry=DateTime.MinValue,started=DateTime.MinValue;
    try {
     Log(log,"Waiting for game character; PID="+pid);
     while(!game.HasExited){
      game.Refresh();string current=Role(process,baseAddress);IntPtr window=game.MainWindowHandle;
      bool restart=relay!=null&&(current!=role||(hook!=null&&!relay.PipeReady)||(!relay.Connected&&(hook!=null||relay.SubscriptionFinished||(DateTime.UtcNow-started).TotalSeconds>15)));
      if(restart){string pending;while((pending=relay.Next())!=null)Log(log,pending);if(hook!=null){hook.Dispose();hook=null;}relay.Dispose();relay=null;retry=DateTime.UtcNow.AddSeconds(1);Log(log,"Character or connection changed; reconnecting.");}
      if(relay==null&&current!=null&&window!=IntPtr.Zero&&DateTime.UtcNow>=retry){role=current;relay=new Relay(pid,server,12050,account,password,role);relay.Start();started=DateTime.UtcNow;Log(log,"Authenticating friend service.");}
      if(relay!=null){
       if(relay.Connected&&relay.PipeReady&&hook==null){try {hook=new PanelHook(dll,window,pid);hook.Initialize();Log(log,"Native friend hook installed.");}catch(Exception){if(hook!=null){hook.Dispose();hook=null;}relay.Dispose();relay=null;retry=DateTime.UtcNow.AddSeconds(1);Log(log,"Native hook failed; retrying.");}}
       if(relay!=null){string msg;while((msg=relay.Next())!=null)Log(log,msg);}
      }
      Thread.Sleep(300);
     }
    }finally{if(hook!=null)hook.Dispose();if(relay!=null)relay.Dispose();CloseHandle(process);Log(log,"Game exited; automatic friends stopped.");}
   }
  }
 }
}
}
