using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace com_mc
{
	//UI通过数据名称唯一的访问
	//上传的数据通过适配器转换为通用的数据协议（类NMEA）,查询所有数据项，符合条件的更新
	//////////////////////////////////////////////////////////////////

	public enum PRO_METHOD //处理类型
	{   //线性处理，按位处理
		pro_val, pro_bit
	}
	public abstract class ProtDom //协议域纯虚父类
	{
		public MC_Prot p_mcp=null; //协议系统的引用
		public string name { get; set; } //名称(唯一)
		public int id { get; set; } = 0; //参数的唯一id，在C程序中使用
		public DataType dtype { get; set; } //数据类型
		public string ref_name { get; set; } //引用协议域或参数的名称
		public ProtDom(Dictionary<string, object> v, DataType t) //从json构造对象
		{ //这里遇到错误就throw出去，不想throw的才判断
			if (v.ContainsKey("id")) id = (int)v["id"];
			name = (string)v["name"];
			dtype = t;
			ref_name = (string)v["ref_name"];
		}
		public virtual List<string> get_children() { return new List<string>(); } //获得本对象的所有子协议域id
		public abstract void pro(byte[] b, int n, ref int off); //处理数据，输入对象首地址，对象长度和当前偏移位置。
		public static Int64 double_2_s64(double f)
		{
			if (f < 0) f -= 0.5;
			else f += 0.5;
			return (Int64)f;
		}
	}
	public class PD_Node : ProtDom //协议域叶子节点
	{
		public DATA_UNION data = new DATA_UNION() { du8 = new byte[8] };
		public int len = 0; //缓存本域的数据长度
		public double pro_k { get; set; } //处理变换kx+b
		public double pro_b { get; set; } //处理变换kx+b
		public PD_Node(Dictionary<string, object> v, DataType t) : base(v, t)
		{
			if(v.ContainsKey("pro_k")) pro_k = (double)v["pro_k"];
			if(v.ContainsKey("pro_b")) pro_b = (double)v["pro_b"];
			switch (dtype)
			{
				case DataType.u8: len = 1; break;
				case DataType.u16: len = 2; break;
				case DataType.u32: len = 4; break;
				case DataType.u64: len = 8; break;
				case DataType.s8: len = 1; break;
				case DataType.s16: len = 2; break;
				case DataType.s32: len = 4; break;
				case DataType.s64: len = 8; break;
				case DataType.f: len = 4; break;
				case DataType.df: len = 8; break;
				default: break;
			}
		}
		//输入二进制值 处理成对应的类型后变换，然后根据输出类型，构造对应的二进制数，若有浮点变整型，需要四舍五入，通过set_val二进制接口传出  
		public override void pro(byte[] b, int n, ref int off) //n：本次处理的长度，off：当前偏移位置
		{
			int rec_off = off; //记录此时的off
			//先自己解析
			data.du64 = 0;
			for (int i = 0; i < len && i < n && off < b.Length; i++)
			{
				data.du8[i] = b[off]; off++;
			}
			//然后做运算
			double d = data.get_double(dtype);
			d = d * pro_k + pro_b;
			//最后给引用的参数
			ParaValue_Val p = (ParaValue_Val)p_mcp.para_dict[ref_name]; //二进制为值类型，输出也必然是值类型
			switch (p.type)
			{
				case DataType.u8:
				case DataType.u16:
				case DataType.u32:
				case DataType.u64:
				case DataType.s8:
				case DataType.s16:
				case DataType.s32:
				case DataType.s64: 
					p.data.ds64=ProtDom.double_2_s64(d); //转换成整数，四舍五入
					break;
				case DataType.f:
					p.data.f = (float)d;
					break;
				case DataType.df:
					p.data.df = d;
					break;
				default:
					p.set_val(b, rec_off, n); //按字节直接给到参数上
					break;
			}
		}
	}
	public class PD_Bit : PD_Node //按位取的叶子节点
	{
		public int bit_len { get; set; } //bit长度
		public PD_Bit(Dictionary<string, object> v, DataType t) : base(v, t)
		{

		}
		public override void pro(byte[] b, int n, ref int off)
		{
			
		}
	}
	public class PD_Array : ProtDom //数组型叶子节点，包括未定义和字符串，输出类型只能是未定义或字符串
	{
		public PD_Array(Dictionary<string, object> v, DataType t) : base(v, t)
		{

		}
		public override void pro(byte[] b, int n, ref int off)
		{

		}
	}
	public class PD_Str : PD_Node //字符型叶子节点，分为十进制和hex，输出类型只能是值类型
	{
		public PD_Str(Dictionary<string, object> v, DataType t) : base(v, t)
		{

		}
		public override void pro(byte[] b, int n, ref int off)
		{

		}
	}
	public class MC_Prot //测控参数体系的实现
	{
		public Dictionary<string, ParaValue> para_dict = new Dictionary<string, ParaValue>(); //参数字典
		public Dictionary<string, ProtDom> prot_dict = new Dictionary<string, ProtDom>(); //协议字典

	}
		public string prot_name { get; set; } //协议名
		public int prot_l { get; set; } //协议tab数量
		public int prot_off { get; set; } //协议中的位置
		public SrcType stype { get; set; } //源数据的类型
		public PRO_METHOD pro_method{get;set; } //处理方法
		public int pro_bit { get; set; } //处理bit的位数(起始)
		public int end_bit { get; set; } //处理bit的位数（终止,包含）
		public double pro_k { get; set; } //处理变换kx+b
		public double pro_b { get; set; } //处理变换kx+b
		public int point_n { get; set; } //小数位数
		public string[] str_tab { get; set; } //显示字符串表
		public bool is_cv { get; set; } //是否显示曲线
		public bool is_dis { get; set; } //是否显示，若是按钮的从属，则可以不显示

		public string cur_str; //当前值
		public double cur_val; //当前值
		public int cur_di; //当前整数值
		public void set_i_val(int di) //直接设置整数值
		{
			cur_di = di;
			set_f_val(di);
		}
		public void set_f_val(double df) //直接设置浮点值
		{
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
					v |= (uint)(cur_di & (1 << i));
					i++;
				} while (i <= end_bit);
				v >>= pro_bit;
				cur_val = v;
			}
			if (dtype == DestType.str) //输出字符型
			{
				uint o = (uint)cur_val;
				if (o < str_tab.Length) cur_str = str_tab[o];
				else cur_str = "";
			}
			update_cb(name); //调用回调函数
		}
		public string val //以文本方式设置，或读取文本值时使用
		{
			get
			{
				if(dtype==DestType.str) return cur_str;
				if(Math.Abs(cur_val-cur_di)<1e-9) return cur_di.ToString();
				return cur_val.ToString(string.Format("F{0}",point_n));
			}
			set
			{
				double df = 0;
				int di = 0;
				//首先按输入类型区分
				if(stype== SrcType.df) df = double.Parse(value);
				else if(stype== SrcType.hex)
				{
					cur_di = int.Parse(value, System.Globalization.NumberStyles.HexNumber);
					df = cur_di;
				}
				else //若是整型
				{
					bool b=int.TryParse(value,out cur_di);
					switch (stype) //若是值型的
					{
						case SrcType.u32: df = (uint)cur_di; break;
						case SrcType.s32: df = cur_di; break;
						case SrcType.u16: df = (ushort)cur_di; break;
						case SrcType.s16: df = (short)cur_di; break;
						case SrcType.u8: df = (byte)cur_di; break;
						case SrcType.s8: df = (sbyte)cur_di; break;
						case SrcType.str: //若源类型是字符
							if (dtype == DestType.str) //且输出字符型
							{
								cur_str = value;
								update_cb(name); //调用回调函数
							}
							return;
						default:
							return;
					}
					if (!b) throw new Exception("");
				}
				set_f_val(df);
			}
		}
		public int update_times = 0; //刷新倒计时

		public DataDes()
		{
			name="";
			dtype=DestType.val;
			prot_name="";
			prot_l=0;
			prot_off=0;
			stype=SrcType.df;
			pro_method=PRO_METHOD.pro_val;
			pro_bit = 0;
			pro_k =1;
			pro_b=0;
			point_n=2; //小数位数默认为2位
			str_tab=new string[] { "关","开" };
			is_cv = false;
			is_dis = true;

			update_cb =void_fun;
			update_dis=void_fun;
		}
		public delegate void CB(string name);
		public void void_fun(string name){}
		public CB update_cb; //数据接收回调
		public CB update_dis; //定时显示回调
	}
}

