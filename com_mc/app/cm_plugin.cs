using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace com_mc
{
	public class CM_Plugin_Interface
	{
		public delegate void TX_CB(byte[] b); //发送回调函数，主程序数据源接受任意格式二进制协议
		//接收回调函数，主程序处理二进制协议，输入整个接收数组b、当前帧起始偏移off、帧长n，对应的协议族根节点号rootid
		public delegate void RX_BIN_CB(byte[]b,int off,int n,int rootid);
		public TX_CB tx_cb = null; //上位机向设备发送的回调函数
		public RX_BIN_CB rx_bin_cb = null; //上位机接收设备信息的回调函数

		public List<Sync_head> binsyn_list =new List<Sync_head>(); //帧同步对象，插件中也可以不用
		public Sync_head cur_binsyn= null; //当前有效的二进制帧同步对象
		public Sync_Line syn_line = null; //帧同步对象，插件中也可以不用
		//public virtual void ini(TX_CB tx,RX_CB rx, RX_BIN_CB  rxbin) //初始化，注册回调函数
		public virtual void ini(TX_CB tx,RX_BIN_CB  rx) //初始化，注册回调函数
		{
			tx_cb = tx;
			//rx_cb = rx;
			rx_bin_cb = rx;
			//有插件，可做额外初始化
			//……
		}
		public virtual void fromJson(Dictionary<string, object> v) //从配置初始化，在ini之后
		{
			binsyn_list.Clear();
			if (v.ContainsKey("syn_bin")) //若有二进制
			{
				ArrayList list = v["syn_bin"] as ArrayList;
				foreach (var item in list)
				{
					var syn_head = new Sync_head();
					syn_head.fromJson(item as Dictionary<string, object>);
					syn_head.rx_bin_cb = rx_bin_cb;
					binsyn_list.Add(syn_head);
				}
			}
			if (v.ContainsKey("syn_line")) //若有文本行
			{
				syn_line = new Sync_Line();
				syn_line.fromJson(v["syn_line"] as Dictionary<string, object>);
				syn_line.rx_bin_cb = rx_bin_cb;
				//syn_line.rx_bin_cb = line_rx_from_bin;
			}
			else if(binsyn_list.Count<=0)//若都没配置，使用默认的文本行模式
			{
				syn_line = new Sync_Line();
				syn_line.rx_bin_cb = rx_bin_cb;
			}
			if(binsyn_list.Count > 0 && syn_line!=null) //若是二进制、文本兼容模式
			{
				//for(int i=0;i< binsyn_list.Count-1;i++) //依次调用各帧同步对象的失锁
				//{
				//	binsyn_list[i].lostlock = binsyn_list[i + 1].rec_byte;
				//}
				//binsyn_list[binsyn_list.Count-1].lostlock = syn_line.rec_byte;
				binsyn_list[0].lostlock = syn_line.rec_byte;
			}
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
			//if (binsyn_list.Count > 0)
			//{
			//	for (int i = 0; i < buf.Length; i++)
			//	{
			//		if (cur_binsyn != null) //若有帧同步对象
			//		{
			//			if (cur_binsyn.rec_p == 0) cur_binsyn = null; //已经同步完成了，去掉
			//		}
			//		if (cur_binsyn != null) cur_binsyn.rec_byte(buf[i]); //若有帧同步对象
			//		else //若需要找用哪个对象
			//		{
			//			foreach (var item in binsyn_list)
			//			{
			//				if (item.SYNC[0] == buf[i]) //若是这个对象
			//				{

			//				}
			//			}
			//		}
			//	}
			//}
			if (binsyn_list.Count > 0) binsyn_list[0].pro(buf, 0, buf.Length);
			else syn_line.pro(buf, 0, buf.Length);
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
