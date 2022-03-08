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
		public TX_CB tx_cb = null; //回调函数
		public RX_CB rx_cb = null; //回调函数

		public Encoding cur_encoding = Encoding.Default; //默认编码
		protected List<byte> rxbuf = new List<byte>(); //串口接收缓冲
		public virtual void ini(TX_CB tx,RX_CB rx) //初始化，注册回调函数
		{
			tx_cb = tx;
			rx_cb = rx;
			//有插件，可做额外初始化
			//……
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
			//无插件，转换为文本
			for (int i = 0; i < buf.Length; i++)
			{
				string s = "";
				rxbuf.Add(buf[i]);
				if (buf[i] == 0x0a)
				{
					s = cur_encoding.GetString(rxbuf.ToArray(), 0, rxbuf.Count);
					rxbuf.Clear();
				}
				if (s != "")
				{
					rx_cb(s);
				}
			}
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
