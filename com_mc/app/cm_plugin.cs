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
		public delegate void RX_BIN_CB(byte[]b,int off,int n,int rootid, bool is_inc);
		public TX_CB tx_cb = null; //上位机向设备发送的回调函数
		public RX_BIN_CB rx_bin_cb = null; //上位机接收设备信息的回调函数

		public MC_Prot p_mcp = null; //动态协议对象的引用
		public Sync_Line syn_line = null; //帧同步对象，插件中也可以不用
		public virtual void ini(MC_Prot pmc, TX_CB tx,RX_BIN_CB rx) //初始化，注册回调函数
		{
			p_mcp = pmc;
			tx_cb = tx;
			//rx_cb = rx;
			rx_bin_cb = rx;
			foreach(var it in p_mcp.prot_root_list) //为每个根节点帧同步对象的回调函数赋值
			{
				it.rx_bin_cb = rx_bin_cb;
			}
			//有插件，可做额外初始化
			//……
		}
		public virtual void fromJson(Dictionary<string, object> v) //从配置初始化，在ini之后
		{
			if (v.ContainsKey("syn_line")) //若有文本行
			{
				syn_line = new Sync_Line();
				syn_line.fromJson(v["syn_line"] as Dictionary<string, object>);
				syn_line.rx_bin_cb = rx_bin_cb;
				//syn_line.rx_bin_cb = line_rx_from_bin;
			}
			else if(p_mcp.prot_root_list.Count<=0)//若都没配置，使用默认的文本行模式
			{
				syn_line = new Sync_Line();
				syn_line.rx_bin_cb = rx_bin_cb;
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
			//无插件，做帧同步（有二进制就不用文本）
			if (p_mcp.prot_root_list.Count > 0) //二进制帧同步对象的处理
			{
				p_mcp.pro_inc(buf,0,buf.Length);
			}
			else syn_line.frame_syn_pro(buf, 0, buf.Length); //文本处理
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
