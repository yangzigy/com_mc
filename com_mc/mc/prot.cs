using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Web.Script.Serialization;
using System.IO;
using System.Windows;

namespace com_mc
{
	//协议相当于结构体定义中的一个变量，协议名称是变量名称。
	//由于每个变量都需要引用参数，所以不需要定义变量的类型。嵌套结构体通过全部展开定义基础数据结构的方式来定义，协议对象无法复用
	public abstract class ProtDom //协议域纯虚父类
	{
		public MC_Prot p_mcp=null; //协议系统的引用
		public PD_Obj father_Dom = null; //上级协议的引用
		public string name { get; set; } = ""; //协议域名称(唯一，或没有)
		public int id { get; set; } = 0; //参数的唯一id，在C程序中使用
		public DataType type { get; set; } = 0; //数据类型,默认是u8（此处无效，在factory处设置为u64）
		public string ref_name { get; set; } = "";//引用参数的名称
		public ParaValue ref_para=null; //引用的参数
		public ProtDom(Dictionary<string, object> v, DataType t, MC_Prot pd) //从json构造对象
		{ //这里遇到错误就throw出去，不想throw的才判断
			p_mcp = pd;
			if (v.ContainsKey("id")) id = (int)v["id"];
			if (v.ContainsKey("name")) name = (string)v["name"];
			type = t;
			while(v.ContainsKey("ref_name"))
			{
				ref_name = (string)v["ref_name"];
				if (!p_mcp.para_dict.ContainsKey(ref_name)) break; //引用的参数不对，按没有算
				ref_para = p_mcp.para_dict[ref_name]; //参数表先于协议加载
				return;
			}
			//没有引用参数，说明是协议内部结构数据
			var tv = new Dictionary<string, object>();
			tv["type"] = "u64";
			tv["name"] = "";
			tv["len"] = 8;
			ref_para = new ParaValue_Val(tv, DataType.u64); //创建一个内部参数对象
		}
		public virtual string[] get_children() { return new string[0]; } //获得本对象的所有子协议域id
		public abstract void pro(byte[] b, ref int off, int n); //处理数据，输入对象首地址，off：数据起始位置，n:off之后还有多长。
		public static Int64 double_2_s64(double f) //浮点转整型四舍五入
		{
			if (f < 0) f -= 0.5;
			else f += 0.5;
			return (Int64)f;
		}
	}
	public class PD_Node : ProtDom //协议域叶子节点
	{
		public DATA_UNION data = new DATA_UNION(); //被协议域引用的时候可以直接用
		public int len = 0; //缓存本域的数据长度
		public int skip_n = 0; //处理完此列后，额外向后跳的字节数（文本行协议是列数）。例如文本行的一列要处理出多个参数，此处可填0。或者需要跳过一列，此处可填2
		public double pro_k { get; set; } = 1; //处理变换kx+b
		public double pro_b { get; set; } = 0;//处理变换kx+b
		public int bit_st { get; set; } = 0; //起始bit
		public int bit_len { get; set; } = 0; //bit长度，用此配置表示此域为按bit处理
		public int bit_singed { get; set; } = 0; //是否是有符号数
		public PD_Node(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			if(v.ContainsKey("pro_k")) pro_k = (double)(decimal)v["pro_k"];
			if(v.ContainsKey("pro_b")) pro_b = (double)(decimal)v["pro_b"];
			if (v.ContainsKey("bit_st")) bit_st = (int)v["bit_st"]; //默认从0bit开始
			if (v.ContainsKey("bit_len")) bit_len = (int)v["bit_len"];
			if (v.ContainsKey("bit_singed")) bit_st = (int)v["bit_singed"]; //默认从0bit开始
			if (v.ContainsKey("skip_n")) skip_n = (int)v["skip_n"];
			len = DATA_UNION.get_type_len(type);
		}
		//输入二进制值 处理成对应的类型后变换，然后根据输出类型，构造对应的二进制数，若有浮点变整型，需要四舍五入，通过set_val二进制接口传出  
		public override void pro(byte[] b, ref int off, int n)  //n:off之后还有多长，off：数据起始位置
		{
			//先自己解析
			int i=data.set_val(b,off,len);
			off += i + skip_n;
			set_para_val(); //设置参数数值
		}
		public void set_para_val() //设置参数值，根据bit_len判断是从df取得还是从du64取得。
		{
			//做运算：根据不同的输入类型，都转换成double来做运算。
			double d = 0;
			if (bit_len > 0) //若配了bit长度，说明是按位处理
			{
				data.du64 >>= bit_st;
				data.du64 &= masktab[bit_len];
				if (bit_singed != 0) //若是有符号数，需要给符号位
				{
					int shift_n = 64 - bit_len;
					data.du64 <<= shift_n;
					data.ds64 >>= shift_n;
					d = data.ds64;
				}
				d = data.du64;
			}
			else d = data.get_double(type); //整数或浮点都能兼容
			d = d * pro_k + pro_b;
			//最后给引用的参数
			ParaValue_Val p = (ParaValue_Val)ref_para; //二进制为值类型，输出也必然是值类型
			p.set_val(d);
		}
		static UInt64[] masktab = new UInt64[] 
		{
			0,
			1, 3, 7, 0xf, 0x1f, 0x3f, 0x7f,0xff,
			0x1ff,0x3ff,0x7ff,0xfff, 0x1fff,0x3fff,0x7fff,0xffff,
			0x1ffff,0x3ffff,0x7ffff,0xfffff, 0x1fffff,0x3fffff,0x7fffff,0xffffff,
			0x1ffffff,0x3ffffff,0x7ffffff,0xfffffff, 0x1fffffff,0x3fffffff,0x7fffffff,0xffffffff,
			0x1ffffffff,0x3ffffffff,0x7ffffffff,0xfffffffff, 0x1fffffffff,0x3fffffffff,0x7fffffffff,0xffffffffff,
			0x1ffffffffff,0x3ffffffffff,0x7ffffffffff,0xfffffffffff, 0x1fffffffffff,0x3fffffffffff,0x7fffffffffff,0xffffffffffff,
			0x1ffffffffffff,0x3ffffffffffff,0x7ffffffffffff,0xfffffffffffff, 0x1fffffffffffff,0x3fffffffffffff,0x7fffffffffffff,0xffffffffffffff,
			0x1ffffffffffffff,0x3ffffffffffffff,0x7ffffffffffffff,0xfffffffffffffff, 0x1fffffffffffffff,0x3fffffffffffffff,0x7fffffffffffffff,0xffffffffffffffff,
		};
	}
	public class PD_Array : ProtDom //数组型叶子节点，包括未定义和字符串，输出类型只能是未定义或字符串
	{
		public int len = 0; //本域的数据长度
		public PD_Array(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			len = (int)v["len"];
		}
		public override void pro(byte[] b, ref int off, int n) //n:off之后还有多长，off：数据起始位置
		{
			n = n >len ? len : n;
			n=ref_para.set_val(b,off,n); //返回使用的字节数
			off += n;
		}
	}
	public class PD_Str : PD_Node //字符型叶子节点，分为十进制和hex，输出类型只能是值类型
	{
		public string str_type = ""; //空为10进制，hex：16进制
		public PD_Str(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			if(v.ContainsKey("str_type")) str_type=v["str_type"] as string;
		}
		public override void pro(byte[] b, ref int off, int n) //字符型在二进制流中取得
		{
			int str_len = len;
			if(str_len == 0) //若不定长度,就按0为分割，确定字符串长度
			{
				for(;str_len<n;str_len++)
				{
					if(b[str_len+off] == 0) break;
				}
			}
			string s = Encoding.UTF8.GetString(b, off,str_len);
			off += str_len;
			pro_str(s);
		}
		public void pro_str(string s) //处理字符串
		{
			if(ref_para.type == DataType.undef || ref_para.type==DataType.str) //串型的输出
			{
				byte[] vs = Encoding.UTF8.GetBytes(s);
				ref_para.set_val(vs,0,vs.Length);
			}
			else //值类型
			{
				if(str_type == "hex") //若是hex型字符串
				{
					data.du64= UInt64.Parse(s, System.Globalization.NumberStyles.HexNumber);
				}
				else if (bit_len > 0) //若配了bit长度，说明是按位处理，需要按整数取而不是浮点
				{
					data.ds64 = Int64.Parse(s);
				}
				else data.df=double.Parse(s); //无论是否是整数，只要按10进制来取就行，当浮点取
				set_para_val();
			}
		}
	}
	public class PD_Switch : PD_Obj //选择协议域方式
	{
		public string ref_type = ""; //引用的协议域，用于确定包类型
		public string cfg_str_type = ""; //配置中，协议索引的字符串类型。空为10进制，hex为10进制
		public Dictionary<int, ProtDom> prot_map=new Dictionary<int, ProtDom>(); //各协议描述符头部，由int对协议进行索引
		public PD_Switch(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			ref_type = v["ref_type"] as string;
			if(v.ContainsKey("cfg_str_type")) cfg_str_type = v["cfg_str_type"] as string; //
			var dict = v["prot_map"] as Dictionary<string, object>; //各协议由字典组织
			foreach (var item in dict)
			{
				int k = 0;
				if(cfg_str_type=="hex")
				{
					k= int.Parse(item.Key, System.Globalization.NumberStyles.HexNumber);
				}
				else int.TryParse(item.Key, out k);
				var tv = item.Value as Dictionary<string, object>;
				var p = MC_Prot.factory(tv, p_mcp); //递归创建自己的子协议域
				p.father_Dom = this; //给上层引用赋值
				prot_map[k] = p;
			}
		}
		public override void pro(byte[] b, ref int off, int n)
		{
			PD_Node pn = father_Dom.prot_dict[ref_type] as PD_Node; //引用的一定是个值类型的协议域
			var para = pn.ref_para as ParaValue_Val;
			int ti = (int)para.data.du64; //此时是变换以后的
			prot_map[ti].pro(b, ref off, n); //找到这个协议，调用
		}
	}
	public class PD_Loop : PD_Obj //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		public string ref_len = ""; //引用的协议域，用于确定重复次数
		public PD_Loop(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			if(v.ContainsKey("ref_len")) ref_len=v["ref_len"] as string;
			else //需要直接指定
			{
				var para = ref_para as ParaValue_Val;
				para.data.ds32 = (int)v["loop_n"];
			}
		}
		public override void pro(byte[] b, ref int off, int n)
		{
			int ti = 0;
			if (ref_len!="") //若是指定的
			{
				PD_Node pn = father_Dom.prot_dict[ref_len] as PD_Node; //引用的一定是个值类型的协议域
				var para = pn.ref_para as ParaValue_Val;
				ti= para.data.ds32; //此时是变换以后的
			}
			else
			{
				var para = ref_para as ParaValue_Val;
				ti = para.data.ds32; //此时是变换以后的
			}
			var pr= prot_dict.ToArray()[0].Value; //取得第一个协议域
			int pre_off = off; //上次偏移位置
			for (int i = 0; i < ti; i++)
			{
				pr.pro(b,ref off, n);
				n -= off - pre_off; //增加了多少字节，总字节数相应减掉
				pre_off = off;
			}
		}
	}
	public class PD_Obj : ProtDom //协议对象
	{
		public List<string> prot_list=new List<string>(); //一系列顺序的协议域
		public Dictionary<string, ProtDom> prot_dict = new Dictionary<string, ProtDom>(); //协议字典
		public PD_Obj(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t,pd)
		{
			if (!v.ContainsKey("prot_list")) return;
			ArrayList list =v["prot_list"] as ArrayList;
			foreach (var item in list)
			{
				var tv = item as Dictionary<string, object>;
				string s;
				if(tv.ContainsKey("name")) s = tv["name"] as string;
				else s = name + "._" + prot_list.Count.ToString(); //若没有显式指明名称，分配自动名称
				prot_list.Add(s);
				var p=MC_Prot.factory(tv,p_mcp); //递归创建自己的子协议域
				p.father_Dom = this; //给上层引用赋值
				p.name = s; //覆盖子节点的名称
				prot_dict[s] = p;
			}
		}
		public override string[] get_children()
		{
			return prot_list.ToArray();
		}
		public override void pro(byte[] b, ref int off, int n)
		{
			int pre_off=off; //上次偏移位置
			for (int i = 0; i < prot_list.Count; i++)
			{
				ProtDom pd=prot_dict[prot_list[i]]; //当前子协议域
				pd.pro(b,ref off, n); //递归调用
				n -= off - pre_off; //增加了多少字节，总字节数相应减掉
				pre_off = off;
			}
		}
	}
	public class PD_LineObj : ProtDom //文本行协议对象
	{
		public List<PD_Str> prot_list = new List<PD_Str>(); //一系列顺序的协议域
		public string[] split_char_list = new string[0]; //本协议的特殊分隔符
		public PD_LineObj(Dictionary<string, object> v, DataType t, MC_Prot pd) : base(v, t, pd)
		{
			if (v.ContainsKey("split_char_list"))
			{
				ArrayList l = v["split_char_list"] as ArrayList;
				split_char_list=new string[l.Count];
				for(int i=0;i<l.Count;i++)
				{
					split_char_list[i]=l[i] as string;	
				}
			}
			ArrayList list = v["prot_list"] as ArrayList;
			foreach (var item in list)
			{
				var tv = item as Dictionary<string, object>;
				DataType tp = DataType.df; //默认类型是double
				if (tv.ContainsKey("type"))
				{
					string ts = MC_Prot.json_ser.Serialize(tv["type"]);
					tp = MC_Prot.json_ser.Deserialize<DataType>(ts); //取得参数类型
				}
				var p = new PD_Str(tv,tp, p_mcp); //递归创建自己的子协议域
				p.father_Dom = null; //给上层引用赋值
				prot_list.Add(p);
			}
		}
		public override string[] get_children()
		{
			List<string> ls = new List<string>();
			for(int i=0;i< prot_list.Count;i++)
			{
				ls.Add(prot_list[i].name);
			}
			return ls.ToArray();
		}
		public override void pro(byte[] b, ref int off, int n)
		{
			
		}
		public void pro_cols(string[] ss) //输入列的列表，用于文本处理
		{
			int col = 0; //处理的列数
			for (int i = 0; i < prot_list.Count && col<ss.Length; i++) //按协议域遍历
			{
				if(prot_list[i].ref_para.name!="")	prot_list[i].pro_str(ss[col]); //若此列有意义
				col += prot_list[i].skip_n + 1; //处理下一个列
			}
		}
	}
	public class MC_Prot //测控参数体系的实现
	{
		public Dictionary<string, ParaValue> para_dict = new Dictionary<string, ParaValue>(); //参数字典
		public ProtDom prot_root=null; //二进制协议根节点
		//文本协议按行分隔，按列分隔，各协议之间按协议名称区分，协议名称为第一列文本，以$开头，加-列数。若第一列不是$开头，则名称仅为-列数
		public Dictionary<string, PD_LineObj> textline_dict = new Dictionary<string, PD_LineObj>(); //文本协议字典
		public void formJson(Dictionary<string, object> v) //初始化
		{
			if (v.ContainsKey("para_dict"))
			{
				ArrayList list = v["para_dict"] as ArrayList;
				foreach (var item in list)
				{
					var tv = item as Dictionary<string, object>;
					string s = tv["name"] as string;
					para_dict[s] = ParaValue.factory(tv); //构建参数
				}
			}
			if (v.ContainsKey("prot_root"))
			{
				prot_root = MC_Prot.factory(v["prot_root"] as Dictionary<string, object>, this); //递归创建自己的子协议域
			}
			//文本行协议
			if(v.ContainsKey("textline_dict"))
			{
				ArrayList list = v["textline_dict"] as ArrayList;
				foreach (var item in list)
				{
					var tv = item as Dictionary<string, object>;
				
					string s = "";
					if (tv.ContainsKey("name"))	s = tv["name"] as string;
					int col_n = (int)tv["col_n"]; //必须描述此协议的列数
					s+="-"+col_n.ToString();
					textline_dict[s] = new PD_LineObj(tv, DataType.str, this);
				}
			}
		}
		public Dictionary<string, object> toJson() //使用json保存当前配置
		{
			Dictionary<string, object> v=new Dictionary<string, object>();
			//先存储变量字典
			var t= new Dictionary<string, object>();
			foreach (var item in para_dict)
			{
				t[item.Key] = item.Value;
			}
			v["para_dict"]= t;

			return v;
		}
		public void clear()
		{
			prot_root = null;
			textline_dict.Clear();
			para_dict.Clear();
		}
		public void pro(byte[] b,int off,int n) //处理二进制包，缓存，偏移，长度（off之后）
		{
			prot_root.pro(b,ref off,n);
		}
		public void pro_line(string s) //处理一行文本
		{
			string[] vs = s.Split(", \t".ToCharArray(), StringSplitOptions.None); //有分隔符没数据也算
			if (vs.Length >= 1) //若有数据
			{
				//构造协议名
				string pname="";
				if (vs[0].StartsWith("$")) pname = vs[0]; //.Substring(1);
				pname+="-"+vs.Length.ToString();
				if(textline_dict.ContainsKey(pname)) //若有这个名字的协议
				{
					if(textline_dict[pname].split_char_list.Length>0)//看这个协议是否指定了分隔符
					{
						vs = s.Split(textline_dict[pname].split_char_list, StringSplitOptions.None);
					}
					textline_dict[pname].pro_cols(vs);
				}
			}
		}

		public static JavaScriptSerializer json_ser = new JavaScriptSerializer();
		public static ProtDom factory(Dictionary<string, object> v, MC_Prot pd) //构建工厂，输入配置，测控协议对象
		{
			string s = json_ser.Serialize(v["type"]); //这样取得的字符串带"
			DataType t = DataType.u64; //默认类型
			try
			{
				t = json_ser.Deserialize<DataType>(s); //取得参数类型，enum类型的反串行化需要字符串带"
			}
			catch (Exception e) //若不是基础类型，则建立协议组织类型
			{
				s=s.Replace("\"", ""); //去掉"
				switch (s)
				{
					case "obj": return new PD_Obj(v, t,pd);
					case "switch": return new PD_Switch(v, t, pd);
					case "loop": return new PD_Loop(v, t, pd);
					default:
						break;
				}
			}
			switch (t) //若是基础类型
			{
				case DataType.undef: return new PD_Array(v, t, pd);
				case DataType.str: return new PD_Str(v, t, pd);
				case DataType.u8:
				case DataType.u16:
				case DataType.u32:
				case DataType.u64:
				case DataType.s8:
				case DataType.s16:
				case DataType.s32:
				case DataType.s64:
				case DataType.f:
				case DataType.df: return new PD_Node(v, t, pd);
				default: throw new Exception("type err");
			}
		}
	}
}

