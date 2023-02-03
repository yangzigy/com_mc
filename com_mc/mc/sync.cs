using System;
using System.Collections.Generic;
using cslib;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections;
using System.Windows;

namespace com_mc
{
	public interface Sync_interfase //帧同步接口
	{
		void fromJson(Dictionary<string, object> v); //初始化配置
		void pro(byte[] b, int off, int n); //数据输入接口
		int prot_root_id(int rootid = -1); //获取或设置本帧同步对象关联的协议族根节点的id（输入-1为查询，大于等于0为设置）
	}
	public enum CHECK_TYPE //数据包校验和的类型
	{
		none=0, //无校验
		sum,crc16,modbuscrc //加和，crc-ccitt，modbus-crc
	}
	public class Sync_head : Frame_Sync, Sync_interfase //帧头同步
	{
		public int ref_prot_root_id = 0; //关联的协议族根节点id，默认为0
		public PD_Node len_dom; //确定长度数据
		public int len_dom_off=0; //确定长度数据的偏移位置
		public Dictionary<int, int> len_dict = new Dictionary<int, int>(); //确定长度数据表，用于包类型与包长的对应
		public CHECK_TYPE check_type =0; //

		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public delegate void PRO_BYTE(byte b); //处理一个字节的回调
		static public void void_pro_byte(byte b) { }
		public PRO_BYTE lostlock= void_pro_byte; //失锁回调
		public CM_Plugin_Interface.RX_BIN_CB rx_bin_cb = null;
		public void fromJson(Dictionary<string,object> v)
		{
			if(v.ContainsKey("prot_root_id")) ref_prot_root_id = (byte)(int)v["prot_root_id"]; //关联的协议族根节点id，默认为0
			if (v.ContainsKey("check_type")) //配置校验类型
			{
				string s = json_ser.Serialize(v["check_type"]);
				check_type = json_ser.Deserialize<CHECK_TYPE>(s); //取得参数类型
			}
			if(v.ContainsKey("syn")) //配置同步字
			{
				ArrayList list = v["syn"] as ArrayList;
				SYNC=new byte[list.Count];
				for(int i=0;i< list.Count; i++) //读取同步字列表
				{ //是16进制
					string s = list[i] as string;
					s = s.Replace("0x", "");
					SYNC[i]=byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
				}
			}
			if(v.ContainsKey("pre_offset")) pre_offset = (byte)(int)v["pre_offset"]; //配置确定包长的偏移位置
			if (v.ContainsKey("pack_len")) pack_len = (int)v["pack_len"]; //配置包长
			else pack_len = pre_offset+1;
			if (v.ContainsKey("len_dom_off")) len_dom_off = (int)v["len_dom_off"]; //
			if(v.ContainsKey("len_dom"))
			{
				var vt = v["len_dom"] as Dictionary<string, object>;
				string s = json_ser.Serialize(vt["type"]);
				ProtType t = json_ser.Deserialize<ProtType>(s); //取得参数类型
				len_dom = new PD_Node(vt, t,null); //一定没有引用参数，所以不使用测控架构对象
			}
			if(v.ContainsKey("len_dict")) //读取长度列表
			{
				var vd = v["len_dict"] as Dictionary<string, object>;
				foreach (var item in vd)
				{
					string s = item.Key;
					int k = 0;
					if (s.StartsWith("0x")) //若是16进制
					{
						k = int.Parse(s.Substring(2), System.Globalization.NumberStyles.HexNumber);
					}
					else k=int.Parse(s);
					len_dict[k]= (int)item.Value;
				}
			}
		}
		public int prot_root_id(int rootid=-1) //获取或设置本帧同步对象关联的协议族根节点的id（输入-1为查询，大于等于0为设置）
		{
			if(rootid>=0) ref_prot_root_id = rootid;
			return ref_prot_root_id;
		}
		public void pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++)
			{
				rec_byte(b[i + off]);
			}
		}
		public override int pre_pack(byte[] b, int len)
		{
			int off = len_dom_off;
			len_dom.pro(b, ref off, len-off);
			int n =(int) len_dom.data.get_double((DataType)len_dom.type);
			if (len_dict.ContainsKey(n)) //若有查表
			{
				n = len_dict[n];
			}
			return n;
		}
		public override int pro_pack(byte[] b, int len)
		{
			switch(check_type) //按校验类型进行校验
			{
				case CHECK_TYPE.sum:
					byte sum=Tool.check_sum(b, len-1);
					if (sum != b[len - 1]) return 1;
					break;
				case CHECK_TYPE.crc16:
					UInt16 crc=Tool.crc_ccitt(b, len-2);
					DATA_UNION d=new DATA_UNION();
					d.du8=b[len-2];
					d.du8_1=b[len - 1];
					if (crc != d.du16) return 1;
					break;
				case CHECK_TYPE.modbuscrc:
					break;
			}
			rx_bin_cb(b, 0, len, ref_prot_root_id);
			return 0;
		}
		public override void lostlock_cb(byte b)
		{
			lostlock(b);
		}
	}
	public class Sync_Line : Line_Sync, Sync_interfase //分行处理
	{
		public int ref_prot_root_id = 0; //关联的协议族根节点id，默认为0
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public CM_Plugin_Interface.RX_BIN_CB rx_bin_cb = null;
		public void fromJson(Dictionary<string, object> v)
		{
			if (v.ContainsKey("prot_root_id")) ref_prot_root_id = (byte)(int)v["prot_root_id"]; //关联的协议族根节点id，默认为0
		}
		public int prot_root_id(int rootid = -1) //获取或设置本帧同步对象关联的协议族根节点的id（输入-1为查询，大于等于0为设置）
		{
			if (rootid >= 0) ref_prot_root_id = rootid;
			return ref_prot_root_id;
		}
		public void pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++)
			{
				rec_byte(b[i + off]);
			}
		}
		public override bool pro_pack(byte[] b, int len)
		{
			rx_bin_cb(b, 0, len,ref_prot_root_id);
			return true;
		}
	}

	public class Sync_Prot : Sync_interfase //增量动态协议帧同步
	{
		public byte[] rec_buff = new byte[256];
		public int rec_p = 0;//偏移指示
		public ProtDom rootpd = null; //本协议族根节点的引用
		public void fromJson(Dictionary<string, object> v)
		{
			
		}
		public int prot_root_id(int rootid = -1) //获取或设置本帧同步对象关联的协议族根节点的id（输入-1为查询，大于等于0为设置）
		{
			return 0;
		}
		public void pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++) rec_byte(b[i + off]);
		}
		public void rec_byte(byte b)
		{
			int pback = 0; //回溯位置，在缓存中的偏移
			int l = 0; //回溯长度
			while (true)
			{

				//if (rec_p < SYNC.Length)//正在寻找包头
				//{
				//	rec_buff[rec_p++] = b;
				//	if (b != SYNC[rec_p - 1])//引导字错误
				//	{
				//		for (int i = 0; i < rec_p; i++) lostlock_cb(rec_buff[i]);
				//		rec_p = 0;
				//	}
				//}
				//else if (rec_p == pre_offset)//可以改变包长
				//{
				//	rec_buff[rec_p++] = b;
				//	pack_len = pre_pack(rec_buff, rec_p);
				//}
				//else//正常接收数据包
				//{
				//	rec_buff[rec_p++] = b;
				//	if (rec_p >= pack_len)
				//	{
				//		int r = 0;
				//		try //若用户处理异常，不影响帧同步
				//		{
				//			r = pro_pack(rec_buff, pack_len); //调用处理函数
				//		}
				//		catch { }
				//		if (r != 0) //若接收不正确
				//		{
				//			if (l == 0) //若还没开始回溯
				//			{
				//				l = rec_p - 1; //回溯长度,用rec_p可能大于pack_len
				//			}
				//			pback = 1; //回溯位置
				//		}
				//		rec_p = 0;
				//	}
				//}
				if (l != 0) //若有回溯任务
				{
					b = rec_buff[pback];
					pback++; l--;
				}
				else return;
			}
		}
	}
}

