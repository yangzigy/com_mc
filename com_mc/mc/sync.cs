using System;
using System.Collections.Generic;
using cslib;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
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
		public PRO_BYTE lostlock; //失锁回调
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
				object[] list = v["syn"] as object[];
				SYNC=new byte[list.Length];
				for(int i=0;i< list.Length;i++)
				{
					SYNC[i]=(byte)list[i];
				}
			}
			if(v.ContainsKey("pre_offset")) pre_offset = (byte)v["pre_offset"]; //配置确定包长的偏移位置
			if (v.ContainsKey("pack_len")) pack_len = (byte)v["pack_len"]; //配置包长
			if (v.ContainsKey("len_dom_off")) len_dom_off = (byte)v["len_dom_off"]; //
			if(v.ContainsKey("len_dom"))
			{
				var vt = v["len_dom"] as Dictionary<string, object>;
				string s = json_ser.Serialize(vt["type"]);
				DataType t = json_ser.Deserialize<DataType>(s); //取得参数类型
				len_dom = new PD_Node(v["len_dom"] as Dictionary<string, object>, t,null); //一定没有引用参数，所以不使用测控架构对象
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
			return (int)(len_dom.data.get_double(len_dom.type));
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

