using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
namespace FlightIslandFriend {
public class Notice { public string Kind, Text; public uint Serial; public FriendRow[] Rows; }
public class FriendRow { public string Name, Account, Group, State; public uint Level; }
public sealed class Reader {
 readonly BinaryReader r; public Reader(byte[] b) { r=new BinaryReader(new MemoryStream(b)); }
 public uint U(){return r.ReadUInt32();} public ulong L(){return r.ReadUInt64();} public byte B(){return r.ReadByte();} public ushort H(){return r.ReadUInt16();}
 public byte[] Bytes(int n){if(n<0 || n>1048576)throw new InvalidDataException("数据长度无效");var b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return b;}
 public string S(){uint first=B(),m=first&3,n;if(m==0)n=first>>2;else if(m==1)n=(first|((uint)B()<<8))>>2;else if(m==2)n=(first|((uint)B()<<8)|((uint)B()<<16)|((uint)B()<<24))>>2;else n=U();if(n>16383)throw new InvalidDataException("文本过长");return Encoding.Unicode.GetString(Bytes(checked((int)n*2)));}
 public Reader Obj(uint header){if(U()!=header)throw new InvalidDataException("协议对象无效");return new Reader(Bytes(checked((int)U())));}
 public void End(){if(r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("协议对象长度不匹配");}
}
public sealed class Bridge:IDisposable {
 string host,user,pass,name;int port; volatile bool stopped; TcpClient subscription; readonly object gate=new object();
 public readonly ConcurrentQueue<Notice> Events=new ConcurrentQueue<Notice>();
 public Bridge(string h,int p,string u,string pw,string n){host=h;port=p;user=u;pass=pw;name=n;}
 void Info(string k,string t){Events.Enqueue(new Notice{Kind=k,Text=t});}
 static byte[] Exact(Stream s,int n){if(n<0||n>1048576)throw new InvalidDataException("数据长度无效");byte[] b=new byte[n];int p=0;while(p<n){int k=s.Read(b,p,n-p);if(k==0)throw new EndOfStreamException("服务端连接已关闭");p+=k;}return b;}
 static byte[] Packet(Action<BinaryWriter> f){using(var m=new MemoryStream()){var w=new BinaryWriter(m,Encoding.UTF8);f(w);w.Flush();return m.ToArray();}}
 static void Utf8(BinaryWriter w,string v){byte[] b=Encoding.UTF8.GetBytes(v);if(b.Length>1024||v.IndexOf('\0')>=0)throw new ArgumentException("登录字段无效");w.Write((uint)b.Length);w.Write(b);}
 public static byte[] Envelope(byte[] b){return Packet(w=>{w.Write(0x001FCA34u);w.Write((uint)b.Length);w.Write(b);w.Write(0x008A119Eu);});}
 public static byte[] Obj(uint h,byte[] b){return Packet(w=>{w.Write(h);w.Write((uint)b.Length);w.Write(b);});}
 public static Reader Unwrap(byte[] b){var r=new Reader(b);if(r.U()!=0x001FCA34u)throw new InvalidDataException("协议头无效");var data=r.Bytes(checked((int)r.U()));if(r.U()!=0x008A119Eu)throw new InvalidDataException("协议尾无效");r.End();return new Reader(data);}
 byte[] Response(NetworkStream s){var h=Exact(s,8);uint status=BitConverter.ToUInt32(h,0);byte[] b=Exact(s,checked((int)BitConverter.ToUInt32(h,4)));if(status!=0)throw new IOException(status==1?"认证失败：请核对账号、密码，并确保该角色已进入游戏。":"服务端拒绝连接，状态="+status);return b;}
 TcpClient Open(uint mode){var c=new TcpClient();try{var a=c.BeginConnect(host,port,null,null);if(!a.AsyncWaitHandle.WaitOne(5000)){c.Close();throw new IOException("连接服务端超时");}c.EndConnect(a);c.ReceiveTimeout=5000;c.SendTimeout=5000;var bytes=Packet(w=>{w.Write(Encoding.ASCII.GetBytes("FIFR"));w.Write(1u);w.Write(mode);Utf8(w,user);Utf8(w,pass);Utf8(w,name);w.Write(1u);});c.GetStream().Write(bytes,0,bytes.Length);return c;}catch{c.Close();throw;}}
 public void Start(){var t=new Thread(Subscribe);t.IsBackground=true;t.Start();}
 void Subscribe(){try{var c=Open(2);lock(gate){if(stopped){c.Close();return;}subscription=c;}Response(c.GetStream());c.ReceiveTimeout=0;Info("connected","好友通知已连接");Refresh();while(!stopped){var h=Exact(c.GetStream(),8);uint code=BitConverter.ToUInt32(h,0);byte[] data=Exact(c.GetStream(),checked((int)BitConverter.ToUInt32(h,4)));if(code==0x1005)Events.Enqueue(ParseRequest(data));}}catch(Exception e){if(!stopped)Info("disconnected",e.Message);}finally{lock(gate){if(subscription!=null)subscription.Close();subscription=null;}}}
 public static Notice ParseRequest(byte[] b){var e=Unwrap(b);var ev=e.Obj(0x656F0101u);e.End();var r=ev.Obj(0x72660101u);ev.End();uint serial=r.U();r.S();r.U();r.U();r.U();r.U();r.U();string account=r.S(),nick=r.S();r.U();string msg=r.S();r.B();r.End();return new Notice{Kind="request",Serial=serial,Text=nick+"（账号 "+account+"）申请添加你为好友。"+(msg.Length>0?"\n留言："+msg:"")};}
 byte[] Call(uint code,Action<BinaryWriter> fields){using(var c=Open(1)){byte[] body=Packet(w=>{w.Write(1u);fields(w);});byte[] env=Envelope(Obj(0x666F0101u,body));byte[] p=Packet(w=>{w.Write(code);w.Write((uint)env.Length);w.Write(env);});c.GetStream().Write(p,0,p.Length);return Response(c.GetStream());}}
 static Reader Function(byte[] b){var r=Unwrap(b);var f=r.Obj(0x666F0101u);r.End();if(f.U()!=1)throw new IOException("操作未成功：申请可能已处理，或角色权限不匹配。");return f;}
 public void Confirm(uint serial,byte answer){var t=new Thread(()=>{try{var r=Function(Call(0x4004,w=>{w.Write(serial);w.Write(answer);w.Write(0u);}));r.End();Info("status",answer==1?"已同意添加好友":answer==2?"已拒绝申请":"申请保留，下次连接可重新查看");Refresh();}catch(Exception e){Info("error",e.Message);}});t.IsBackground=true;t.Start();}
 public void Refresh(){var t=new Thread(()=>{try{var rows=ParseList(Call(0x4001,w=>{w.Write(0x00017007u);w.Write(1u);}));Events.Enqueue(new Notice{Kind="list",Rows=rows});}catch(Exception e){Info("error",e.Message);}});t.IsBackground=true;t.Start();}
 static int Count(Reader r){uint n=r.U();if(n>10000)throw new InvalidDataException("列表过长");return (int)n;}
 public static FriendRow[] ParseList(byte[] b){var f=Function(b);var rows=new List<FriendRow>();int n=Count(f);for(int i=0;i<n;i++){var c=f.Obj(0x63690201u);c.U();c.U();c.U();string group=c.S();int count=Count(c);for(int j=0;j<count;j++)rows.Add(Row(c,group));c.B();c.B();c.End();}n=Count(f);for(int i=0;i<n;i++)rows.Add(Row(f,"未分组"));f.End();return rows.ToArray();}
 static FriendRow Row(Reader parent,string group){var r=parent.Obj(0x75690301u);string account=r.S();r.L();uint flags=r.U();r.S();uint status=r.U();r.U();r.U();r.H();r.S();var a=r.Obj(0x61690101u);a.B();a.U();a.End();r.L();r.U();r.U();r.U();r.U();string nick=r.S();r.S();r.B();uint level=r.U();r.U();r.B();r.B();r.B();r.End();return new FriendRow{Name=nick,Account=account,Group=group,Level=level,State=(flags&1)!=0?"已屏蔽":(flags&2)!=0?"待确认":status==10?"在线":"离线"};}
 public Notice Next(){Notice n;return Events.TryDequeue(out n)?n:null;}
 public void Dispose(){stopped=true;lock(gate){if(subscription!=null)subscription.Close();}}
}
}
