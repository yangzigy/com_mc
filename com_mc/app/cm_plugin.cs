using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace com_mc
{
	public class CM_Plugin_Interface
	{
		public delegate void TX_CB(byte[] b); //发送回调函数，主程序数据源接受任意格式二进制协议
		public delegate void RX_CB(string s); //接收回调函数，主程序接收仅处理标准文本行协议
		public delegate void RX_BIN_CB(byte[]b,int off,int n); //接收回调函数，主程序处理二进制协议
		public TX_CB tx_cb = null; //上位机向设备发送的回调函数
		public RX_CB rx_cb = null; //上位机接收设备信息的回调函数
		public RX_BIN_CB rx_bin_cb = null; //上位机接收设备信息的回调函数

		public Encoding cur_encoding = Encoding.Default; //默认编码
		public Sync_head syn_head =null; //帧同步对象
		public Sync_Line syn_line = null; //帧同步对象
		protected List<byte> rxbuf = new List<byte>(); //串口接收缓冲
		public virtual void ini(TX_CB tx,RX_CB rx, RX_BIN_CB  rxbin) //初始化，注册回调函数
		{
			tx_cb = tx;
			rx_cb = rx;
			rx_bin_cb = rxbin;
			//有插件，可做额外初始化
			//……
		}
		public virtual void fromJson(Dictionary<string, object> v) //从配置初始化，在ini之后
		{
			if (v.ContainsKey("syn_bin")) //若是二进制
			{
				syn_head = new Sync_head();
				syn_head.fromJson(v["syn_bin"] as Dictionary<string, object>);
				syn_head.rx_bin_cb = rx_bin_cb;
			}
			else if (v.ContainsKey("syn_lin")) //若有文本行
			{
				syn_line = new Sync_Line();
				syn_line.fromJson(v["syn_lin"] as Dictionary<string, object>);
				syn_line.rx_bin_cb = line_rx_from_bin;
			}
			else //若都没配置，使用默认的文本行模式
			{
				syn_line = new Sync_Line();
				syn_line.rx_bin_cb = line_rx_from_bin;
			}
			if(syn_head!=null && syn_line!=null) //若是二进制、文本兼容模式
			{
				syn_head.lostlock = syn_line.rec_byte;
			}
		}
		public void line_rx_from_bin(byte[] b, int off, int n) //从二进制包变为文本行
		{
			string s = cur_encoding.GetString(b, off, n);
			if (s != "") rx_cb(s);
		}
		public virtual void send_cmd(string s) //主程序发送指令
		{
			//无插件，转换为二进制的直接发出
			var b = Encoding.UTF8.GetBytes(s + "\n");
			tx_cb(b);
			//有插件，插件可截获指令，选择发出，或者变更协议
			//……
		}
		public virtual void rx_fun(byte[] buf) //接收数据函数
		{
			//无插件，做帧同步
			if(syn_head!=null) syn_head.pro(buf,0, buf.Length);	
			else syn_line.pro(buf,0, buf.Length);
			//有插件，可进行协议转换
			//……
		}
		public virtual void so_poll_100() //周期调用,100Hz
		{
			//有插件，可进行周期处理
			//……
		}
	}
}
