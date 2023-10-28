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
		void frame_syn_pro(byte[] b, int off, int n); //数据输入接口
	}
	public enum CHECK_TYPE //数据包校验和的类型
	{
		none=0, //无校验
		sum,crc16,modbuscrc //加和，crc-ccitt，modbus-crc
	}
#region 可配置帧同步库
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
		public void frame_syn_pro(byte[] b, int off, int n)
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
					UInt16 crc=Tool.crc_ccitt(b,0, len-2);
					DATA_UNION d=new DATA_UNION();
					d.du8=b[len-2];
					d.du8_1=b[len - 1];
					if (crc != d.du16) return 1;
					break;
				case CHECK_TYPE.modbuscrc:
					break;
			}
			rx_bin_cb(b, 0, len, ref_prot_root_id,false); //定长的调用，后级可直接调用协议实体的处理
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
		public void frame_syn_pro(byte[] b, int off, int n)
		{
			for (int i = 0; i < n; i++)
			{
				rec_byte(b[i + off]);
			}
		}
		public override bool pro_pack(byte[] b, int len)
		{
			rx_bin_cb(b, 0, len,ref_prot_root_id,false);
			return true;
		}
	}
#endregion
	public class Sync_Prot : Sync_interfase //增量动态协议帧同步
	{
		public byte[] rec_buff = new byte[1024 * 1024];
		public int rec_p = 0;//偏移指示(输入数据的)
		public int toff = 0; //偏移指示，处理数据的
		public int ref_prot_root_id = 0; //关联的协议族根节点id，默认为0
		public CM_Plugin_Interface.RX_BIN_CB rx_bin_cb = null;
		public PD_Obj rootpd = null; //本协议族根实体的引用
		public List<ParaValue> para_need_update = new List<ParaValue>(); //需要更新的参数列表
		public Sync_Prot(PD_Obj pd)
		{
			rootpd = pd; //输入帧同步对象对应的根节点。
			//遍历所有叶子节点，给叶子节点的参数列表赋值
			set_para_update(rootpd);
		}
		public void set_para_update(ProtDom pd) //遍历所有叶子节点，给叶子节点的参数列表赋值
		{
			var pdo = pd as PD_Obj;
			var pdn = pd as PD_Node;
			if (pdo != null)
			{
				foreach (var item in pdo.prot_list) //如果是对象，就遍历他的所有子
				{
					set_para_update(item);
				}
			}
			else if (pdn != null) //如果是节点，就给他赋值
			{
				pdn.para_need_update = para_need_update;
			}
		}
		public void fromJson(Dictionary<string, object> v)
		{
			
		}
		public void frame_syn_pro(byte[] b, int off, int n) //帧同步处理
		{
			for (int i = 0; i < n; i++) rec_byte(b[i + off]);
		}
		public void rec_byte(byte b)
		{
			int pback = 0; //回溯位置，在缓存中的偏移
			int back_n = 0; //回溯长度
			while (true)
			{
				rec_buff[rec_p++] = b;
				int r = 0;
				try //若用户处理异常，不影响帧同步
				{
					r = rootpd.pro(rec_buff, ref toff, rec_p-toff); //调用对应协议族的根节点
				}
				catch { r = 2; }
				if (r == 2) //若接收不正确
				{
					para_need_update.Clear();
					rootpd.reset_state(); //没错的域没有被复位，这里统一复位
					if (back_n == 0) //若还没开始回溯
					{
						back_n = rec_p - 1; //回溯长度
					}
					pback = 1; //回溯位置
					toff = 0;
					rec_p = 0;
				}
				if(r==0) //若正确接收
				{
					//首先调用rx函数，记录
					rx_bin_cb(rec_buff,0,rec_p, ref_prot_root_id,true); //增量输入，已经解析完成了，后级只需更新参数即可
					para_need_update.Clear();
					//恢复状态
					toff = 0;
					rec_p = 0;
					rootpd.reset_state(); //正确接收以后内部复位，此处保险
				}
				else //若没有接收完成
				{
				}
				if (back_n > 0) //若有回溯任务
				{
					b = rec_buff[pback];
					pback++; back_n--;
				}
				else return;
			}
		}
	}
}

