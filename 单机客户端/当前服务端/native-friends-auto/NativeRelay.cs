using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
namespace FlightIslandNative {
public sealed class Relay:IDisposable {
 readonly string host,user,password,character,pipeName; readonly int port; readonly object gate=new object();
 volatile bool stopped,connected,pipeReady,subscriptionFinished; TcpClient subscription; NamedPipeServerStream pipe;
 public readonly ConcurrentQueue<string> Messages=new ConcurrentQueue<string>();
 readonly ConcurrentQueue<byte[]> events=new ConcurrentQueue<byte[]>();
 readonly ConcurrentQueue<FlightIslandFriend.Notice> requests=new ConcurrentQueue<FlightIslandFriend.Notice>();
 byte[] snapshot; volatile bool refreshNeeded=true; uint lastStatus=uint.MaxValue;
 public bool Connected {get{return connected;}}
 public bool PipeReady {get{return pipeReady;}}
 public bool SubscriptionFinished {get{return subscriptionFinished;}}
 public Relay(int processId,string h,int p,string u,string pw,string n){pipeName="qq-flight-native-"+processId;host=h;port=p;user=u;password=pw;character=n;}
 static byte[] Exact(Stream s,int n){if(n<0||n>1048584)throw new InvalidDataException("Invalid length");var b=new byte[n];int p=0;while(p<n){int k=s.Read(b,p,n-p);if(k==0)throw new EndOfStreamException();p+=k;}return b;}
 static byte[] Packet(Action<BinaryWriter> a){using(var m=new MemoryStream()){var w=new BinaryWriter(m,Encoding.UTF8);a(w);w.Flush();return m.ToArray();}}
 static void Text(BinaryWriter w,string s){byte[] b=Encoding.UTF8.GetBytes(s);if(b.Length>1024||s.IndexOf('\0')>=0)throw new InvalidDataException("Invalid identity");w.Write((uint)b.Length);w.Write(b);}
 TcpClient Open(uint mode){var c=new TcpClient();try{var a=c.BeginConnect(host,port,null,null);if(!a.AsyncWaitHandle.WaitOne(4000))throw new IOException("连接超时");c.EndConnect(a);c.ReceiveTimeout=4000;c.SendTimeout=4000;var p=Packet(w=>{w.Write(Encoding.ASCII.GetBytes("FIFR"));w.Write(1u);w.Write(mode);Text(w,user);Text(w,password);Text(w,character);w.Write(1u);});c.GetStream().Write(p,0,p.Length);return c;}catch{c.Close();throw;}}
 public byte[] RawCall(uint code,byte[] envelope){if(!((code>=0x4001&&code<=0x4009)||(code>=0x4101&&code<=0x4105)))throw new InvalidDataException("Unsupported function");if(envelope.Length<20||envelope.Length>1048576)throw new InvalidDataException("Invalid envelope");// The native GetFriendList default key is (0,0), meaning the current player.
 // Resolve it only after the relay's per-call online-character authentication.
 if(code==0x4001&&envelope.Length==32&&BitConverter.ToUInt32(envelope,20)==0&&BitConverter.ToUInt32(envelope,24)==0){envelope=(byte[])envelope.Clone();Array.Copy(BitConverter.GetBytes(0x17007u),0,envelope,20,4);Array.Copy(BitConverter.GetBytes(1u),0,envelope,24,4);}
 using(var c=Open(1)){var p=Packet(w=>{w.Write(code);w.Write((uint)envelope.Length);w.Write(envelope);});c.GetStream().Write(p,0,p.Length);var h=Exact(c.GetStream(),8);uint n=BitConverter.ToUInt32(h,4);var data=Exact(c.GetStream(),checked((int)n));if(BitConverter.ToUInt32(h,0)!=0)throw new IOException("服务端认证或调用失败");return data;}}
 public void Start(){new Thread(Subscribe){IsBackground=true}.Start();new Thread(Pipes){IsBackground=true}.Start();new Thread(Snapshots){IsBackground=true}.Start();}
 static byte[] NameBytes(string name){var enc=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);var bytes=enc.GetBytes(name);if(bytes.Length==0||bytes.Length>15||name.IndexOf('\0')>=0)throw new InvalidDataException("角色名无法放入原生 16 字节名字字段："+name);var result=new byte[16];Array.Copy(bytes,result,bytes.Length);return result;}
 public static byte[] EncodeSnapshot(string owner,FlightIslandFriend.FriendRow[] rows){return Packet(w=>{w.Write(NameBytes(owner));var accepted=new System.Collections.Generic.List<FlightIslandFriend.FriendRow>();foreach(var row in rows){if(row.State=="在线"||row.State=="离线")accepted.Add(row);}if(accepted.Count>20)throw new InvalidDataException("原生面板最多支持 20 位好友；未截断服务端名单。");w.Write((uint)accepted.Count);foreach(var row in accepted){w.Write(NameBytes(row.Name));w.Write((byte)(row.State=="在线"?1:0));w.Write((byte)Math.Min(row.Level,99u));w.Write((ushort)0);}});}
 void Snapshots(){while(!stopped){if(connected){refreshNeeded=false;try{byte[] call=FlightIslandFriend.Bridge.Envelope(FlightIslandFriend.Bridge.Obj(0x666f0101u,Packet(w=>{w.Write(1u);w.Write(0x17007u);w.Write(1u);})));var rows=FlightIslandFriend.Bridge.ParseList(RawCall(0x4001,call));byte[] current=EncodeSnapshot(character,rows);lock(gate){snapshot=current;}refreshNeeded=false;}catch(Exception e){Messages.Enqueue("好友名单同步失败："+e.Message);}}for(int i=0;i<10&&!stopped&&!refreshNeeded;i++)Thread.Sleep(100);if(!connected)Thread.Sleep(200);}}
 void Subscribe(){try{var c=Open(2);lock(gate){if(stopped){c.Close();return;}subscription=c;}var h=Exact(c.GetStream(),8);Exact(c.GetStream(),checked((int)BitConverter.ToUInt32(h,4)));if(BitConverter.ToUInt32(h,0)!=0)throw new IOException("认证失败：角色须在线，账号密码和角色名须匹配。");connected=true;refreshNeeded=true;Messages.Enqueue("服务端认证通过；正在主动接入游戏原生面板。");c.ReceiveTimeout=0;while(!stopped){h=Exact(c.GetStream(),8);var b=Exact(c.GetStream(),checked((int)BitConverter.ToUInt32(h,4)));if(BitConverter.ToUInt32(h,0)==0x1005){requests.Enqueue(FlightIslandFriend.Bridge.ParseRequest(b));Messages.Enqueue("收到好友申请，等待游戏窗口确认。");}}}catch(Exception e){if(!stopped)Messages.Enqueue("通知连接断开："+e.Message);}finally{connected=false;subscriptionFinished=true;lock(gate){if(subscription!=null)subscription.Close();subscription=null;}}}
 public byte[] Process(uint code,byte[] data){
  if(code==0xffff0003){if(data.Length!=0)throw new InvalidDataException("Invalid poll");byte[] current;lock(gate){current=snapshot;}if(!connected||current==null)return Packet(w=>{w.Write(1u);w.Write(0u);});FlightIslandFriend.Notice request;requests.TryDequeue(out request);byte[] text=request==null?new byte[0]:Encoding.Unicode.GetBytes(request.Text+"\n\n是：同意；否：拒绝；取消：暂不处理。\0");if(text.Length>2048)text=Encoding.Unicode.GetBytes("收到好友申请。是：同意；否：拒绝；取消：暂不处理。\0");byte[] payload=Packet(w=>{w.Write(current);w.Write(request==null?0u:request.Serial);w.Write((uint)text.Length);w.Write(text);});return Packet(w=>{w.Write(0u);w.Write((uint)payload.Length);w.Write(payload);});}
  if(code==0xffff0002){if(data.Length!=16)throw new InvalidDataException("Invalid panel status");uint status=BitConverter.ToUInt32(data,0);PanelInitialized=status==0;if(status!=lastStatus){lastStatus=status;string message=status==0?"CTMessenger 原生好友列表已同步。":status==17?"情侣戒指：已收到真实背包响应并重新更新原生计数。":status==16?"情侣申请已自动选中持有的戒指。":status==14?"情侣申请窗口已通过游戏连接请求服务端戒指数量。":status==13?"后台好友已接入；打开好友面板后自动同步。":status==1?"未找到 CTMessenger 面板或列表控件；打开橙色好友面板后重连。":status==2?"客户端内存入口不匹配，已停止接入。":status==3?"角色对象未就绪，进入游戏后重连。":status==4?"原生列表状态异常，已停止同步，避免继续修改。":status==5?"所选游戏进程的角色名与填写的角色名不符，已停止同步。":status==8?"好友申请队列已满；待处理申请仍保存在服务端，重连后可重新接收。":status==9?"游戏内申请弹框入口不匹配；申请保留，未代替你确认。":status==10?"游戏内申请弹框创建失败；申请保留，稍后重试。":status==11?"游戏内好友申请弹框已显示：确认同意，取消拒绝，X拒绝申请。":"已找到 CTMessenger，等待服务端好友名单。";Messages.Enqueue(message);}return new byte[8];}
  if(code==0xffff0001){if(data.Length!=8)throw new InvalidDataException("Invalid callback state");bool ready=BitConverter.ToUInt32(data,0)!=0&&BitConverter.ToUInt32(data,4)!=0;if(ready&&!callbackReady){Messages.Enqueue("游戏回调已注册，消息号="+BitConverter.ToUInt32(data,4));}callbackReady=ready;byte[] ev;if(ready&&events.TryDequeue(out ev))return ev;return new byte[8];}
  Messages.Enqueue("原生调用 0x"+code.ToString("X4"));var result=RawCall(code,data);if(code==0x4004){refreshNeeded=true;Messages.Enqueue(BitConverter.ToUInt32(result,16)==1?"好友申请处理成功。":"好友申请处理失败，请重连检查申请状态。");}Messages.Enqueue("原生响应 0x"+code.ToString("X4")+"，"+result.Length+" 字节");return Packet(w=>{w.Write(0u);w.Write((uint)result.Length);w.Write(result);});
 }
 public volatile bool PanelInitialized;
 volatile bool callbackReady;
 void Pipes(){try{while(!stopped){using(var p=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Message,PipeOptions.Asynchronous,1048584,1048584)){lock(gate){if(stopped)return;pipe=p;pipeReady=true;}p.WaitForConnection();try{var h=Exact(p,8);uint code=BitConverter.ToUInt32(h,0);var b=Exact(p,checked((int)BitConverter.ToUInt32(h,4)));var response=Process(code,b);p.Write(response,0,response.Length);p.Flush();p.WaitForPipeDrain();}catch(Exception e){if(!stopped)Messages.Enqueue("原生桥调用失败："+e.Message);}finally{lock(gate){if(pipe==p)pipe=null;}}}}}catch(Exception e){if(!stopped)Messages.Enqueue("原生桥停止："+e.Message);}finally{pipeReady=false;}}
 public string Next(){string s;return Messages.TryDequeue(out s)?s:null;}
 public void Dispose(){stopped=true;lock(gate){if(subscription!=null)subscription.Close();if(pipe!=null)pipe.Dispose();}}
}
}
