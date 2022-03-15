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
	}
	public enum CHECK_TYPE //数据包校验和的类型
	{
		none=0, //无校验
		sum,crc16,modbuscrc //加和，crc-ccitt，modbus-crc
	}
	public class Sync_head : Frame_Sync, Sync_interfase //帧头同步
	{
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
				DataType t = json_ser.Deserialize<DataType>(s); //取得参数类型
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
			int n =(int) len_dom.data.get_double(len_dom.type);
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
					//byte sum=
					//if (sum != b[len - 1]) return 1;
					break;
				case CHECK_TYPE.crc16:
					break;
				case CHECK_TYPE.modbuscrc:
					break;
			}
			rx_bin_cb(b, 0, len);
			return 0;
		}
		public override void lostlock_cb(byte b)
		{
			lostlock(b);
		}
	}
	public class Sync_Line : Line_Sync, Sync_interfase //分行处理
	{
		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public CM_Plugin_Interface.RX_BIN_CB rx_bin_cb = null;
		public void fromJson(Dictionary<string, object> v)
		{

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
			rx_bin_cb(b, 0, len);
			return true;
		}
	}
}

