using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace lgd_gui
{
	//UI通过数据名称唯一的访问
	//上传的数据通过适配器转换为通用的数据协议（类NMEA）,查询所有数据项，符合条件的更新
	//////////////////////////////////////////////////////////////////
	public enum DataType //数据类型
	{   //值，字符
		t_val, t_str
	}
	public enum SrcType //源类型
	{   //浮点，字符，hex，整数
		src_double,src_float, src_str, src_hex, src_int  
	}
	public enum PRO_METHOD //处理类型
	{   //线性处理，按位处理
		pro_val, pro_bit
	}
	public class DataDes //数据描述
	{
		public string name { get; set; } //显示名称(唯一)
		public DataType type {get;set;} //数据类型
		public string prot_name { get; set; } //协议名
		public int prot_l { get; set; } //协议tab数量
		public int prot_off { get; set; } //协议中的位置
		public SrcType src_type { get; set; } //源数据的类型
		public PRO_METHOD pro_method{get;set; } //处理方法
		public int pro_bit { get; set; } //处理bit的位数(起始)
		public int end_bit { get; set; } //处理bit的位数（终止,包含）
		public double pro_k { get; set; } //处理变换kx+b
		public double pro_b { get; set; } //处理变换kx+b
		public string[] str_tab { get; set; } //显示字符串表
		public bool dis_curve { get; set; } //是否显示曲线
		public bool is_dis { get; set; } //是否显示，若是按钮的从属，则可以不显示

		string cur_str; //当前值
		public double cur_val; //当前值
		public string val
		{
			get { return type==DataType.t_val?cur_val.ToString():cur_str;}
			set
			{
				double df = 0;
				int di = 0;
				if (type == DataType.t_str) //若是字符型的
				{
					switch (src_type) //也得看看源数据类型
					{
						case SrcType.src_str:
							cur_str = value;
							update_cb(name); //调用回调函数
							return;
						case SrcType.src_int:
							di = int.Parse(value);
							break;
						case SrcType.src_hex:
							di = int.Parse(value, System.Globalization.NumberStyles.HexNumber);
							break;
						default:
							return;
					}
					if (pro_method == PRO_METHOD.pro_val) //若是值处理
					{
						cur_val = df * pro_k + pro_b;
						uint o = (uint)cur_val;
						if (o < str_tab.Length) cur_str = str_tab[o];
						else cur_str = "";
					}
					else //若是bit处理
					{
						int i = pro_bit;
						uint v = 0;
						do
						{
							v |= (uint)(di & (1 << i));
							i++;
						} while (i<=end_bit);
						v >>= pro_bit;
						if (v < str_tab.Length) cur_str = str_tab[v];
						else cur_str = "";
					}
				}
				else
				{
					switch (src_type) //若是值型的
					{
						case SrcType.src_float:
							df = float.Parse(value);
							break;
						case SrcType.src_double:
							df = double.Parse(value);
							break;
						case SrcType.src_int:
							di = int.Parse(value);
							df = di;
							break;
						case SrcType.src_hex:
							di = int.Parse(value, System.Globalization.NumberStyles.HexNumber);
							df = di;
							break;
						default:
							return;
					}
					if (pro_method == PRO_METHOD.pro_val) //若是值处理
					{
						cur_val = df * pro_k + pro_b;
					}
					else //若是bit处理
					{
						int i = pro_bit;
						uint v = 0;
						do
						{
							v |= (uint)(di & (1 << i));
							i++;
						} while (i <= end_bit);
						v >>= pro_bit;
						cur_val = v;
					}
				}
				update_cb(name); //调用回调函数
			}
		}
		public int update_times = 0; //刷新倒计时

		public DataDes()
		{
			name="";
			type=DataType.t_val;
			prot_name="";
			prot_l=0;
			prot_off=0;
			src_type=SrcType.src_float;
			pro_method=PRO_METHOD.pro_val;
			pro_bit = 0;
			pro_k =1;
			pro_b=0;
			str_tab=new string[] { "关","开" };
			dis_curve = false;
			is_dis = true;

			update_cb =void_fun;
			update_dis=void_fun;
		}
		public delegate void CB(string name);
		public void void_fun(string name){}
		public CB update_cb; //数据接收回调
		public CB update_dis; //定时显示回调
	}
	/////////////////////////////////////////////////////////////////////////
	//命令部分
	public enum CmdType //指令类型
	{
		bt, //按键
		text, //文本框
		sw, //开关
		rpl_bool, //带回复的指令
		label, //文本控件
		para, //参数型
	}
	public class CmdDes //指令描述
	{
		public string name { get; set; } //命令显示名称(唯一)
		public string refdname { get; set; } //关联的数据名称
		public string suffixname { get; set; } //后缀参数名称
		public string cmd { get; set; } //命令名称
		public string cmdoff { get; set; } //关闭指令
		public int c_span { get; set; } //列跨度
		public CmdType type { get; set; } //命令名称
		public string dft { get; set; } //默认值
		public CB_s_v get_stat= void_fun; //获取此指令对象状态的回调函数

		public delegate string CB_s_v();
		public static string void_fun() { return ""; }
		public CmdDes()
		{
			name="";
			refdname = "";
			cmd="";
			cmdoff = "";
			c_span = 1;
			suffixname = "";
			type =CmdType.bt;
			dft="";
			refdname = "";
		}
	}
	/////////////////////////////////////////////////////////////////////////
	//测控总体
	public class Com_MC //通用测控类
	{
		public Dictionary<string,DataDes> dset { get; set; } //数据列表,key为数据项的名称
		public Dictionary<string,CmdDes> cmds { get; set; } //数据列表,key为数据项的名称
        public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public Com_MC()
		{
			dset = new Dictionary<string, DataDes>();
			cmds = new Dictionary<string, CmdDes>();
		}
		public static Com_MC fromJson(string s)
		{
			return json_ser.Deserialize<Com_MC>(s);
		}
		public string toJson()
		{
			return json_ser.Serialize(this);
		}
		//////////////////////////////////////////////////////
		//协议适配器
		public string prot_adapter(byte[] b) //输入某种协议，输出通用协议
		{
			return "";
		}
		public string prot_adapter(string s) //输入某种字符协议，输出通用协议
		{
			return s;
		}
		///////////////////////////////////////////////////////
		//刷新数据
		public void update_data(string s) //输入通用协议的一行
		{
			string[] vs = s.Split(", \t".ToCharArray(), StringSplitOptions.None);
			if(vs.Length>=1) //若有数据
			{
				//首先通过个数找
				foreach (var item in dset)
				{
					if(item.Value.prot_l==vs.Length) //若数量对了
					{
						if(item.Value.prot_name!="") //有协议名称
						{
							if((!vs[0].StartsWith("$")) || //数据里没有协议
								item.Value.prot_name!=vs[0]) //协议名称不等
							{
								continue;
							}
						}
						item.Value.val = vs[item.Value.prot_off];
					}
				}
			}
		}
	}
}

