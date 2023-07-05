using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using cslib;

namespace com_mc
{
	using JD = Dictionary<string, object>;
	/// <summary>
	/// Prot_Cfg_Window.xaml 的交互逻辑
	/// </summary>
	public partial class Prot_Cfg_Window : Window
	{
		/////////////////数据部分，输入时已经用字符串倒了一手，是全新的    //////////////////////////////////
		//参数实体
		static public Dictionary<string, ParaValue> para_dict = new Dictionary<string, ParaValue>(); //参数字典
		//缓存的结构json字典
		static public JD struct_dict = new JD();
		//根节点的缓存
		public List<string> rootlist = new List<string>();
		//文本点的缓存
		public List<string> tl_root = new List<string>(); //行结构的名称

		/////////////////////////////////////////////////////////////////////////////
		//外部引用
		public MC_Prot cur_prot = null; //主窗口当前使用的协议
		//将协议刷新到界面上，界面变量部分
		List<PEdit_Display> para_disobj = new List<PEdit_Display>(); //测量量的列表
		List<PEdit_Display> prot_treeobj = new List<PEdit_Display>(); //协议域的树形结构，内容不管
		public Prot_Cfg_Window()
		{
			InitializeComponent();
		}
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			Visibility = Visibility.Hidden;
		}
		public void load_prot_from_json(JD v) //从json加载协议
		{
			//这个v复制的不彻底，需要用字符串倒一手，让他彻底复制
			string sj = Tool.json_ser.Serialize(v); //这样取得的字符串带"
			v = Tool.json_ser.Deserialize<JD>(sj); //
			try //转换过程中，不完整的协议配置会在建立联系的时候异常，对于现实来说无所谓
			{
			//1、初始化参数字典
				para_dict.Clear();
				ArrayList list = v["para_dict"] as ArrayList;
				foreach (var item in list)
				{
					var tv = item as JD;
					string s = tv["name"] as string;
					para_dict[s] = ParaValue.factory(tv); //构建参数
				}
			//2、构造结构定义
				struct_dict = v["struct_dict"] as JD;
			//3、根节点初始化
				rootlist.Clear();
				if (v.ContainsKey("prot_roots"))
				{
					ArrayList li = v["prot_roots"] as ArrayList; //读取各协议族的根节点
					foreach (var item in li) //对于每一个根
					{
						string s = item as string;
						rootlist.Add(s);
					}
				}
			//4、初始化文本协议
				tl_root.Clear();
				if (v.ContainsKey("tl_root"))
				{
					var tv = v["tl_root"] as JD;
					tv["type"] = "text";
					var tl = tv["prot_list"] as ArrayList;
					foreach (var item in tl)
					{
						tl_root.Add(item as string);
					}
				}
			}
			catch (Exception ex) 
			{
				MessageBox.Show(ex.Message,"加载错误");
			}
			update_paralist_display(); //首先刷新参数字典
			update_protlist_display(); //刷新协议域
			update_rootslist_display(); //刷新协议根节点列表
			update_textswitch_display(); //刷新文本协议
		}
		public void update_paralist_display() //更新参数列表的显示
		{
			para_disobj.Clear();
			foreach (var item in para_dict)
			{
				PEdit_Display tl = new PEdit_Display();
				tl.name = item.Value.name;
				tl.type = item.Value.type.ToString();
				tl.len = item.Value.len.ToString();
				para_disobj.Add(tl);
				//dg_vir.Items.Add(tl); //这样加双击时报无法修改错误
			}
			dg_para.ItemsSource = null;
			dg_para.ItemsSource = para_disobj; //刷新到界面上
		}
		public void update_protlist_display() //更新协议树的显示
		{
			//首先保存当前treeview中的折叠状态
			List<string> expand_list=new List<string>();
			foreach (PEdit_Display item in tv_prot.Items)
			{
				TreeViewItem tvi = tv_prot.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
				if (tvi!=null && tvi.IsExpanded == true) expand_list.Add(item.name);
			}
			//重新构造显示数据结构
			prot_treeobj.Clear(); //专门用于显示的数据结构
			foreach (var item in struct_dict) //对于每一个定义的顶层结构
			{
				PEdit_Display tl = new PEdit_Display();
				var tv = item.Value as JD;
				tv["name"] = item.Key;
				tl.create_recu(tv, null);//为此节点加子节点
				prot_treeobj.Add(tl);
			}
			//刷新显示
			tv_prot.ItemsSource = null;
			tv_prot.ItemsSource = prot_treeobj;
			//恢复折叠状态
			foreach (PEdit_Display item in tv_prot.Items)
			{
				TreeViewItem tvi = tv_prot.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
				if (expand_list.Exists(x => x == item.name)) tvi.IsExpanded = true;
			}
		}
		public void update_rootslist_display() //刷新协议族根节点的显示
		{
			List<PEdit_Display> roots_dis = new List<PEdit_Display>();
			foreach (var item in rootlist)
			{
				PEdit_Display tl = new PEdit_Display();
				tl.name = item;
				try
				{ //在树形结构中找到这个结构的描述对象，取得type
					tl.type = (from x in prot_treeobj where x.name == item select x.type).ElementAt(0);
				}
				catch
				{
					tl.type = "";
				}
				roots_dis.Add(tl);
			}
			dg_roots_list.ItemsSource = null;
			dg_roots_list.ItemsSource = roots_dis;
		}
		public void update_textswitch_display() //刷新文本协议列表的显示
		{
			List<PEdit_Display> text_dis = new List<PEdit_Display>();
			foreach (var item in tl_root)
			{
				PEdit_Display tl = new PEdit_Display();
				tl.name = item;
				try
				{ //在树形结构中找到这个结构的描述对象，并引用描述对象的属性对象，取得列数
					tl.len = (from x in prot_treeobj 
							  where x.name == item 
							  select (x.pd_prop as PD_LineObj_PropDis).col_n).ElementAt(0).ToString();
				}
				catch
				{
					tl.type = "";
				}
				text_dis.Add(tl);
			}
			dg_text_switch.ItemsSource = null;
			dg_text_switch.ItemsSource = text_dis;
		}
		private void mi_open_Click(object sender, RoutedEventArgs e) //打开协议处理
		{
			FrameworkElement fe = sender as FrameworkElement;
			try
			{
				switch (fe.Tag)
				{
					case "cur": //加载当前协议
						{
							if (cur_prot == null) throw new Exception("当前无协议");
							var v = cur_prot.toJson(); //DataDes的额外配置不在协议中写
							load_prot_from_json(v);
							Title = "Prot_Cfg_Window - 当前协议";
						}
						break;
					case "file": //从文件加载协议: 协议配置为.prot ，对应的参数文件为.para
						{
							var ofd = new System.Windows.Forms.OpenFileDialog();
							ofd.Filter = "*.prot|*.prot";
							if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
							string para_fname = System.IO.Path.ChangeExtension(ofd.FileName, "para");
							object jspara = Tool.load_json_from_file<JD>(para_fname);
							object jsprot = Tool.load_json_from_file<Dictionary<string, object>>(ofd.FileName);
							Tool.dictinary_update(ref jsprot, jspara); //更新配置
							//从文件来的是省略json域的，需要实例化一下，然后再转换回来
							MC_Prot tmcp = new MC_Prot();
							tmcp.fromJson(jsprot as JD);
							jsprot = tmcp.toJson();
							load_prot_from_json(jsprot as JD);
							Title = "Prot_Cfg_Window - "+ System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
						}
						break;
				}
			}
			catch (Exception ee)
			{
				MessageBox.Show(ee.ToString(), "错误");
			}
		}
		public string beautify_prot_json(string s) //美化输出的协议配置json
		{
			Regex reg = new Regex("({.*?})"); //最短匹配大括号
			s = "{\n\t" + s.Substring(1, s.Length - 2) + "\n}"; //替换前后的大括号
			s = reg.Replace(s, "\n\t\t$1");
			s = s.Replace("]\n", "\n\t]\n"); //替换最后一个]
			return s;
		}
		public string reduce_json(JD dic, JD topstruct) //简化json，递归输出字符串。输入本协议域的配置，以及顶层结构
		{
			//输入是协议域结构，带有全部域的，仅输出协议域字典的值部分，协议域名称部分不输出
			JD tv = new JD();
			string sout = "{";
			foreach (var item in dic) //遍历协议域内的所有属性
			{
				if (item.Key == "type")
				{
					string stype = item.Value as string;
					if (stype == "obj" || stype == "sw" || stype == "str" || stype == "tline")
					{
						continue;
					}
				}
				if (item.Key == "name") 
				{
					string name = item.Value as string;
					if(name.StartsWith("_")) continue; //名字里带_的不要，顶层名不要
					if (topstruct.ContainsKey(name)) continue;//若顶层结构里有，就不写这个name了
				}
					
				if (item.Key == "len" && ((int)item.Value) == 0) continue; //len为0的应该是默认的
				if (item.Key == "prot_list") //若是子节点
				{
					//首先输出子节点的大括号
					sout += "\"prot_list\":\n\t\t[";
					ArrayList list = item.Value as ArrayList;
					for (int i = 0; i < list.Count; i++)
					{
						string s = list[i] as string; //直接给了这个协议域的名称，需要是之前定义过的，在结构列表中找。
						var subv = list[i] as JD;
						if (subv != null) //若是直接在此定义了简短的协议域
						{
							string ts = "";
							if (subv.ContainsKey("name")) //对于子节点，如果名称与顶层结构名相同，就只记录一个名称
							{
								ts = subv["name"] as string;
							}
							if (ts!="" && topstruct.ContainsKey(ts))
							{
								s = string.Format("\"{0}\",",ts);
							}
							else s = reduce_json(subv,topstruct) +",";
						}
						else //若是引用的协议域，就是一个名称
						{
							s = "\"" + s + "\",";
						}
						sout += "\n\t\t\t"+s;
					}
					sout = sout.Remove(sout.Length - 1); //删掉最后的逗号
					sout += "\n\t\t],";
				}
				else //若是叶子节点
				{
					JD tmpv = new JD();
					tmpv[item.Key] = item.Value;
					//tv[item.Key] = item.Value; //这里只能复制string int等基础类型的type
					string ts = Tool.json_ser.Serialize(tmpv);
					if (ts.Length <= 2) continue;
					ts=ts.Substring(1, ts.Length - 2); //去掉前后的大括号
					sout += ts + ",";
				}
			}
			if (sout[sout.Length-1]==',') sout=sout.Remove(sout.Length - 1); //删掉最后的逗号
			sout += "}";
			return sout;
		}
		string get_array_string(List<string> li) //获得字符数组的字符串格式化
		{
			string sout = "[";
			for (int i = 0; i < li.Count; i++) //将每个域变成字符串
			{
				sout += string.Format("\"{0}\",", li[i]);
			}
			sout=sout.Remove(sout.Length - 1); //删掉最后的逗号
			sout += "]";
			return sout;
		}
		private void mi_save_Click(object sender, RoutedEventArgs e) //存储协议配置
		{
			var ofd = new System.Windows.Forms.SaveFileDialog();
			ofd.Filter = "*.prot|*.prot";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
			string para_fname=System.IO.Path.ChangeExtension(ofd.FileName,"para");
			JD vstruct = new JD();
			//简化json域
			string sout = "{\n\t";
			if(tl_root.Count>0)
			{
				sout += string.Format("\"tl_root\": {{\"prot_list\":{0}}},\n\t", get_array_string(tl_root));
			}
			if (rootlist.Count > 0)
			{
				sout += string.Format("\"prot_roots\": {0},\n\n\t", get_array_string(rootlist));
			}
			sout += "\"struct_dict\":\n\t{\n";
			foreach (var item in struct_dict) //结构是个字典，需要一个一个来简化
			{
				string ts = reduce_json(item.Value as JD,struct_dict); //每一个顶层结构
				ts=string.Format("\t\t\"{0}\":{1},\n",item.Key,ts); //"AA_02": {……}
				sout += ts;
			}
			sout=sout.Remove(sout.Length - 2); //删掉最后的回车和逗号
			sout += "\n\t}"; //struct_dict的结束
			sout += "\n}\n"; //整个文件结束
			//根据简化后的协议配置生成协议文件的内容
			var v= MC_Prot.toJson(para_dict, vstruct, rootlist, tl_root); //这样只有para有用
			//保存参数文件
			JD vp = new JD();
			vp["para_dict"] = v["para_dict"];
			StreamWriter sw = new StreamWriter(para_fname);
			string s = Tool.json_ser.Serialize(vp);
			s = beautify_prot_json(s);
			sw.Write(s);
			sw.Close();
			//结构存入文件
			v.Remove("para_dict"); //剩下的都在协议文件中
			sw = new StreamWriter(ofd.FileName);
			sw.Write(sout);
			sw.Close();
			
		}
		private void mi_save_as_cur_Click(object sender, RoutedEventArgs e) //写入当前协议
		{
			
		}
#region 参数列表
		private void dg_para_MouseDown(object sender, MouseButtonEventArgs e) //参数列表的鼠标按下
		{
			try //若上次显示的属性不是参数
			{
				var t = pg_prot.SelectedObject as ParaValue_PropDis;
				if (t == null) throw new Exception();
			}
			catch (Exception ee)
			{
				dg_para_SelectedCellsChanged(null, null); //刷新属性显示
			}
		}
		private void dg_para_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
		{
			if (dg_para.SelectedIndex >= 0)
			{
				string name = para_disobj[dg_para.SelectedIndex].name; //取得所选的名称
				var obj = para_dict[name]; //取得所选的对象
				var disobj = ParaValue_PropDis.get_disobj(obj);
				pg_prot.SelectedObject = disobj;
			}
		}
		private void bt_para_add_Click(object sender, RoutedEventArgs e) //参数的添加
		{
			string name = get_untitle_name(); //获得一个没有用过的名字
			Dictionary<string, object> v = new Dictionary<string, object>();
			v["name"] = name; v["type"] = DataType.df;
			var vp = ParaValue.factory(v); //建立一个空的变量
			para_dict[name] = vp;
			update_paralist_display(); //刷新参数列表显示
			for (int i = 0; i < para_disobj.Count; i++) //选中这个变量
			{
				if (para_disobj[i].name==name) //找到这个变量
				{
					dg_para.SelectedIndex = i;
					break;
				}
			}
			dg_para_SelectedCellsChanged(null, null); //刷新属性显示
		}
		private void bt_para_del_Click(object sender, RoutedEventArgs e) //删除参数
		{
			if (dg_para.SelectedItem != null)
			{
				var ped = dg_para.SelectedItem as PEdit_Display;
				string name = ped.name; //取得所选的名称
				var obj = para_dict[name]; //取得所选的对象
				if (MessageBox.Show("确定删除？", "删除参数", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					para_dict.Remove(name);
					update_paralist_display(); //刷新协议域
					pg_prot.SelectedObject = null; //属性控件置空
				}
			}
		}
#endregion
#region 协议树
		private void tv_prot_MouseDown(object sender, MouseButtonEventArgs e) //协议树鼠标按下
		{
			try //若上次显示的属性不是协议
			{
				var t = pg_prot.SelectedObject as ProtDom_PropDis;
				if (t == null) throw new Exception();
			}
			catch (Exception ee)
			{
				tv_prot_SelectedItemChanged(null, null); //刷新属性显示
			}
		}
		private void tv_prot_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) //协议树的选择
		{
			if (tv_prot.SelectedItem !=null)
			{
				var ped = tv_prot.SelectedItem as PEdit_Display;
				pg_prot.SelectedObject = ped.pd_prop;
			}
		}
		void prottree_add(bool is_top) //协议树添加节点，输入是否是顶层节点
		{
			//添加的是实体，而实体是从根生出来的，应指定父节点
			string name = get_untitle_name(); //获得一个没有用过的名字
			var tv = new JD(); //创建一个新的默认变量
			tv["name"] = name;
			tv["type"] = "u8";
			if (tv_prot.SelectedItem == null || is_top) //若没有选择，或指定了在顶层添加
			{
				struct_dict[name] = tv;
			}
			else //若选择了一个节点，则一定是PD_Obj的节点，在此节点以下添加
			{
				var ped = tv_prot.SelectedItem as PEdit_Display;
				if (!(ped.pd_prop is PD_Obj_PropDis) || (ped.pd_prop is PD_Switch_PropDis)) //若不是Obj类型，或是sw类型
				{
					return; //这种类型不能添加直接的子节点
				}
				ArrayList p;
				try { p = ped.pd_prop.p_struct["prot_list"] as ArrayList; } //可能没有这个域
				catch { p= new ArrayList(); }
				p.Add(tv);
				ped.pd_prop.p_struct["prot_list"] = p;
			}
			update_protlist_display();
			foreach (PEdit_Display item in tv_prot.Items)
			{
				TreeViewItem tvi = tv_prot.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
				if (item.name == name) //找到刚这个节点，选中他
				{
					tvi.Focus(); break;
				}
			}
			tv_prot_SelectedItemChanged(null, null); //刷新属性显示
		}
		private void bt_prot_add_Click(object sender, RoutedEventArgs e) //协议树的添加
		{
			prottree_add(false);
		}
		private void bt_prot_addTop_Click(object sender, RoutedEventArgs e) //添加顶层节点
		{
			prottree_add(true);
		}
		private void bt_prot_del_Click(object sender, RoutedEventArgs e) //协议树的节点删除
		{
			if (tv_prot.SelectedItem != null)
			{
				var ped = tv_prot.SelectedItem as PEdit_Display;
				if (MessageBox.Show("确定删除？", "删除协议域", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					if (ped.p_father == null) //若是顶层，删除方法不一样，顶层是字典
					{
						struct_dict.Remove(ped.name);
					}
					else //若是一般结构的子结构，需要在数组中删除，不是字典
					{
						var al = ped.p_father.pd_prop.p_struct["prot_list"] as ArrayList;
						for (int i = 0; i < al.Count; i++)
						{
							string s = al[i] as string; //直接给了这个协议域的名称，需要是之前定义过的，在结构列表中找。
							var tv = al[i] as JD;
							if (tv != null) //若是直接在此定义了简短的协议域
							{
								if ((tv["name"] as string) == ped.name) al.RemoveAt(i);
							}
							else if(s==ped.name) al.RemoveAt(i);//若是定义的复用的结构
						}
					}
					update_protlist_display(); //刷新协议域
					pg_prot.SelectedObject = null; //属性控件置空
				}
			}
		}
#endregion
		private void bt_update_prop_Click(object sender, RoutedEventArgs e) //更新属性点击
		{
			var t = pg_prot.SelectedObject;
			var pe=t as Prop_Edit;
			if(pe.display_2_var()) //让每个显示对象更新对应的后台变量，若需要更新变量类型
			{
				var pv = t as ParaValue_PropDis; //若是参数的显示
				var pp = t as ProtDom_PropDis; //若是协议域显示
				if(pv!=null) //若是参数的显示
				{
					update_paralist_display();
					dg_para_SelectedCellsChanged(null, null); //刷新属性显示
				}
				else if(pp!=null) //若是协议域显示
				{
					update_protlist_display();
					tv_prot_SelectedItemChanged(null, null); //刷新属性显示
				}
			}
		}
		string get_untitle_name() //获得一个未命名的名字，在参数和结构两个字典中都是唯一的
		{
			string s ="";
			int i = 0;
			while (true)
			{
				s = string.Format("未命名{0}", i);
				if (!para_dict.ContainsKey(s) && !struct_dict.ContainsKey(s)) break;
				i++;
			}
			return s;
		}
	}

	public class PEdit_Display //树形结构，内容不管
	{
		//显示的基本信息，是在树形结构中显示用的，不是属性显示用的
		public string name { get; set; }
		public string type { get; set; } //因为是复用的，可能是协议类型，也可能是参数类型
		public string len { get; set; }
		public List<PEdit_Display> sub { get; set; }=new List<PEdit_Display>(); //子节点
		public PEdit_Display p_father =null; //上级节点
		//public JD p_struct=null; //记录自己的结构描述
		public ProtDom_PropDis pd_prop = null; //属性显示对象
		public void create_recu(JD v, PEdit_Display f) //递归加载协议域的配置，形成树形结构.
		{ //这一步时，配置json已经指导过实例化，省略的部分已经都补上了
			p_father = f;
			type = v["type"] as string;
			name = v["name"] as string;
			int l = v.ContainsKey("len")?(int)v["len"]:0; //有些就是没有长度
			len = l.ToString();
			pd_prop = ProtDom_PropDis.factory(name, v);
			pd_prop.p_struct = v; //让属性对象引用结构描述
			if (f != null) pd_prop.p_father = f.pd_prop; //给属性对象的上级节点赋值
			if (pd_prop.type == ProtType.sw) //就switch特殊，没有使用prot_list
			{
				//switch不需要显示子
			}
			else if (pd_prop is PD_Obj_PropDis) //若是其他具有子的协议域
			{
				if (!v.ContainsKey("prot_list")) return;
				ArrayList list = v["prot_list"] as ArrayList;
				foreach (var item in list) //对于子列表中的每个项，可能是字符名称，也可能直接定义了子节点
				{
					PEdit_Display tp = new PEdit_Display();
					string s = item as string; //直接给了这个协议域的名称，需要是之前定义过的，在结构列表中找。
					var tv = item as JD;
					if (tv != null) //若是直接在此定义了简短的协议域
					{
						tp.create_recu(item as JD, this);
					}
					else if (Prot_Cfg_Window.struct_dict.ContainsKey(s)) //若是定义的复用的结构，若没定义就不添加
					{
						tv = Prot_Cfg_Window.struct_dict[s] as JD;
						tp.create_recu(tv, this);
					}
					else continue;
					sub.Add(tp);
				}
			}
		}
	}
	//显示对象是在选择操作的时候现场创建的，树形结构不用这个
	public interface Prop_Edit //属性编辑的接口
	{
		bool display_2_var(); //从显示对象更新到后台变量，返回是否生成了新的协议域
	}
#region 显示结构定义：参数
	public class ParaValue_PropDis : Prop_Edit //在属性修改显示时使用的结构
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = "";  //参数的唯一id，在C#程序中使用
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public DataType type { get; set; } = DataType.df; //参数类型,默认是double(在factory处设置)
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //数据长度
		[CategoryAttribute("常规"), DescriptionAttribute("显示字符表")]
		public List<string> 显示字符表 { get; set; } = new List<string>();//显示字符串表。可用于bool型指令，0为失败字符，1为成果字符
		
		public ParaValue backend_var; //关联的后台变量
		public ParaValue_PropDis(ParaValue v)
		{
			backend_var = v;
			name = v.name; type=v.type; len = v.len;
			显示字符表.Clear();
			显示字符表.AddRange(v.str_tab);
		}
		public virtual bool display_2_var() //从显示对象更新到后台变量
		{
			if(backend_var.type!=type) //若修改过类型，查看是否需要重新建立后台变量
			{
				Dictionary<string, object> v=new Dictionary<string, object>();
				v["name"] = name; v["type"] = type;
				backend_var=ParaValue.factory(v); //建立一个空的变量
				Prot_Cfg_Window.para_dict[name] = backend_var;
				return true;
			}
			if(backend_var.name!=name) //名称不同，需要在字典中改
			{
				Prot_Cfg_Window.para_dict.Remove(backend_var.name);
				backend_var.name = name;
				Prot_Cfg_Window.para_dict[name] = backend_var;
				return true;
			}
			backend_var.type=type; backend_var.len = len;
			backend_var.str_tab.Clear();
			backend_var.str_tab.AddRange(显示字符表);
			return false;
		}
		static public ParaValue_PropDis get_disobj(ParaValue v) //从参数类构造参数的显示对象
		{
			if(v.GetType()== typeof(ParaValue_Str)) //如果是字符型参数
			{
				return new ParaValue_PropDis(v);
			}
			return new ParaValue_Val_PropDis(v as ParaValue_Val);
		}
	}
	public class ParaValue_Val_PropDis : ParaValue_PropDis //在属性修改显示时使用的结构
	{
		[CategoryAttribute("常规"), DescriptionAttribute("保留小数位数")]
		public int point_n { get; set; } = 0; //
		public ParaValue_Val_PropDis(ParaValue_Val v) : base(v)
		{
			point_n = v.point_n;
		}
		public override bool display_2_var()
		{
			if (base.display_2_var()) return true;
			var vt = backend_var as ParaValue_Val;
			vt.point_n= point_n;
			return false;
		}
	}
#endregion
#region 显示结构定义：结构
	public class ProtDom_PropDis : Prop_Edit //ProtDom对象的显示扩充
	{
		[CategoryAttribute("常规"), DescriptionAttribute("名字")]
		public string name { get; set; } = ""; //协议域名称(唯一，或没有)
		[CategoryAttribute("常规"), DescriptionAttribute("类型")]
		public ProtType type { get; set; } = 0; //数据类型,默认是u8（此处无效，在factory处设置为u64）
		[CategoryAttribute("常规"), DescriptionAttribute("引用参数名")]
		public string ref_name { get; set; } = "";//引用参数的名称
		[CategoryAttribute("常规"), DescriptionAttribute("长度")]
		public int len { get; set; } = 0; //数据长度
		public JD p_struct = null; //记录自己的结构描述
		public ProtDom_PropDis p_father=null; //用于树形结构中记录上层
		public ProtDom_PropDis(string s, ProtType t, JD v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			name = s; type = t; 
			if(v.ContainsKey("ref_name")) ref_name = (string)v["ref_name"];
			if (v.ContainsKey("len")) len = (int)v["len"];
		}
		public virtual bool display_2_var() //从显示对象更新到后台变量
		{
			bool r = false;
			var stype = p_struct["type"] as string; //之前的类型
			if (stype != type.ToString()) //若修改过类型，所有域除了name都要去掉
			{
				string tn= p_struct["name"] as string; //之前的名称
				p_struct.Clear();
				p_struct["type"] = type.ToString();
				p_struct["name"] = tn;
				r = true; //需要重新建立后台变量
			}
			var tname= p_struct["name"] as string; //之前的名称
			if (tname != name) //名称不同，需要在字典中改
			{
				p_struct["name"] = name;
				var t0 = p_struct;
				if (p_father != null) //若是子协议域
				{
					var p=p_father.p_struct["prot_list"] as ArrayList; //在其父的各个子中找自己
					for (int i = 0; i < p.Count; i++)
					{
						var tv = p[i] as JD; //由于有直接命名的情况，所以不一定是JD
						string ts = tv==null?p[i] as string:tv["name"] as string; //
						if (ts==tname) //找到了之前的那个名称
						{
							p[i] = t0;
						}
					}
				}
				else //若是顶层协议域
				{
					Prot_Cfg_Window.struct_dict.Remove(tname);
					Prot_Cfg_Window.struct_dict[name] = t0;
				}
				r= true; //需要重新建立后台变量
			}
			return r;
		}
		static public ProtDom_PropDis factory(string nm, JD v) //从协议域构造协议的显示对象
		{
			ProtType t = ProtType.str; //默认类型,str的不需要写type这个域
			bool need_t = false; //是否需要更新t
			if (v.ContainsKey("type"))
			{
				need_t = true;
			}
			else if (v.ContainsKey("prot_list")) //若没指定type，但有子节点
			{
				if (v.ContainsKey("col_n")) v["type"] = "tline"; //若是文本行协议
				else v["type"] = "obj"; //若是obj
				need_t = true;
			}
			if (need_t) //不是默认的t
			{
				string s = Tool.json_ser.Serialize(v["type"]); //这样取得的字符串带"
				t = Tool.json_ser.Deserialize<ProtType>(s); //取得参数类型，enum类型的反串行化需要字符串带"
			}
			switch (t) //若是基础类型
			{
				case ProtType.undef:
				case ProtType.str: return new PD_Str_PropDis(nm,t,v);
				case ProtType.u8:
				case ProtType.u16:
				case ProtType.u32:
				case ProtType.u64:
				case ProtType.s8:
				case ProtType.s16:
				case ProtType.s32:
				case ProtType.s64:
				case ProtType.f:
				case ProtType.df: return new PD_Node_PropDis(nm,t, v);
				case ProtType.obj: return new PD_Obj_PropDis(nm,t, v);
				case ProtType.sw: return new PD_Switch_PropDis(nm,t, v);
				case ProtType.loop: return new PD_Loop_PropDis(nm,t, v);
				case ProtType.text: return new PD_LineSwitch_PropDis(nm,t, v);
				case ProtType.tline: return new PD_LineObj_PropDis(nm,t, v);
				case ProtType.check: return new PD_Check_PropDis(nm, t, v);
				default: throw new Exception("type err");
			}
		}
	}
	public class PD_Node_PropDis : ProtDom_PropDis //协议域叶子节点
	{
		[CategoryAttribute("常规"), DescriptionAttribute("额外向后跳的字节数（文本行协议是列数）")]
		public int skip_n { get; set; } = 0; //处理完此列后，额外向后跳的字节数（文本行协议是列数）。例如文本行的一列要处理出多个参数，此处可填0。或者需要跳过一列，此处可填1
		[CategoryAttribute("常规"), DescriptionAttribute("处理变换kx+b")]
		public double pro_k { get; set; } = 1; //处理变换kx+b
		[CategoryAttribute("常规"), DescriptionAttribute("处理变换kx+b")]
		public double pro_b { get; set; } = 0;//处理变换kx+b
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中的起始bit")]
		public int bit_st { get; set; } = 0; //起始bit
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中的bit长度")]
		public int bit_len { get; set; } = 0; //bit长度，用此配置表示此域为按bit处理
		[CategoryAttribute("常规"), DescriptionAttribute("bit处理中是否有符号位")]
		public bool bit_singed { get; set; } = false; //是否是有符号数
		public PD_Node_PropDis(string s, ProtType t, JD v) : base(s,t,v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("pro_k"))
			{
				try { pro_k = (double)(decimal)v["pro_k"]; }
				catch { pro_k = (double)v["pro_k"]; }
			}
			if (v.ContainsKey("pro_b"))
			{
				try { pro_b = (double)(decimal)v["pro_b"]; }
				catch { pro_b = (double)v["pro_b"]; }
			}
			if (v.ContainsKey("bit_st")) bit_st = (int)v["bit_st"]; //默认从0bit开始
			if (v.ContainsKey("bit_len")) bit_len = (int)v["bit_len"];
			if (v.ContainsKey("bit_singed")) bit_singed = ((int)v["bit_singed"])!=0?true:false; //默认从0bit开始
			if (v.ContainsKey("skip_n")) skip_n = (int)v["skip_n"];
		}
		public override bool display_2_var()
		{
			if(skip_n!=0) p_struct["skip_n"] = skip_n;
			if(Math.Abs(pro_k-1)>1e-9) p_struct["pro_k"] = pro_k;
			if(Math.Abs(pro_b)>1e-9) p_struct["pro_b"] = pro_b;
			if (bit_st != 0) p_struct["bit_st"] = bit_st;
			if(bit_len != 0) p_struct["bit_len"] = bit_len;
			if(bit_singed) p_struct["bit_singed"] = 1;
			p_struct["ref_name"] = ref_name;
			return base.display_2_var();
		}
	}
	public class PD_Str_PropDis : PD_Node_PropDis //字符型叶子节点，分为十进制和hex，输出类型只能是值类型
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议读入方式")]
		public bool is_hex { get; set; } = false; //hex或空
		public PD_Str_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("str_type")) is_hex = (v["str_type"] as string) == "hex" ? true:false;
		}
		public override bool display_2_var()
		{
			p_struct["ref_name"] = ref_name;
			if (is_hex) p_struct["is_hex"] = 1;
			return base.display_2_var();
		}
	}
	public class PD_Obj_PropDis : ProtDom_PropDis //协议对象，由于只是显示属性，不要递归，子节点是在树形结构那边做的
	{
		public PD_Obj_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("ref_name")) v.Remove("ref_name");
		}
	}
	public class PD_Switch_PropDis : ProtDom_PropDis //选择协议域方式
	{
		[CategoryAttribute("常规"), DescriptionAttribute("索引值类型")]
		public bool is_hex { get; set; } = false; //索引值类型hex或空
		[CategoryAttribute("常规"), DescriptionAttribute("引用的协议域")]
		public string ref_prot { get; set; } = "";//引用的协议域名称
		[CategoryAttribute("常规"), DescriptionAttribute("议索引的字符串类型。空为10进制，hex为10进制")]
		public string cfg_str_type { get; set; } = ""; //配置中，协议索引的字符串类型。空为10进制，hex为10进制
		[CategoryAttribute("常规"), DescriptionAttribute("分支后前后跳过的字节数，向前为负")]
		public int skip_n { get; set; } = 0;//
		[CategoryAttribute("常规"), DescriptionAttribute("分支的值和对应的协议名")]
		public Dictionary<int, string> protname_map { get; set; } = new Dictionary<int, string>(); //各协议描述符头部，由int对协议名进行索引

		public PD_Switch_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			ref_prot = v["ref_type"] as string;
			if (v.ContainsKey("cfg_str_type")) cfg_str_type = v["cfg_str_type"] as string; //
			if (v.ContainsKey("skip_n")) skip_n = (int)v["skip_n"];
			var dict = v["prot_map"] as Dictionary<string, object>; //各协议由字典组织
			foreach (var item in dict) //这里记录的是： 整数字符：结构名
			{
				int k = 0;
				if (cfg_str_type == "hex")
				{
					k = int.Parse(item.Key, System.Globalization.NumberStyles.HexNumber);
				}
				else int.TryParse(item.Key, out k);
				//创建子节点
				string s = item.Value as string; //结构名
				protname_map[k] = s;
			}
		}
		public override bool display_2_var()
		{
			p_struct["ref_prot"] = ref_prot;
			if (is_hex) p_struct["is_hex"] = 1;
			if(cfg_str_type!="") p_struct["cfg_str_type"] = cfg_str_type;
			if (skip_n != 0) p_struct["skip_n"] = skip_n;
			return base.display_2_var();
		}
	}
	public class PD_Loop_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("用于确定重复次数的引用协议域")]
		public string ref_len { get; set; } = ""; //用于确定重复次数的引用协议域
		[CategoryAttribute("常规"), DescriptionAttribute("直接指定重复次数")]
		public int loop_n { get; set; } = 0; //直接指定重复次数
		public PD_Loop_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("ref_len"))
			{
				ref_len = v["ref_len"] as string;
			}
			else //需要直接指定
			{
				loop_n = (int)v["loop_n"];
			}
		}
		public override bool display_2_var()
		{
			if (loop_n != 0) p_struct["loop_n"] = loop_n;
			if(ref_len!="") p_struct["ref_len"] = ref_len;
			return base.display_2_var();
		}
	}
	public class PD_LineSwitch_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("编码格式")]
		public Encoding cur_encoding { get; set; } = Encoding.UTF8; //
		public PD_LineSwitch_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("encoding")) cur_encoding = Encoding.GetEncoding(v["encoding"] as string);
		}
		public override bool display_2_var()
		{
			if (cur_encoding != Encoding.UTF8) p_struct["cur_encoding"] = "utf-8";
			return base.display_2_var();
		}
	}
	public class PD_LineObj_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("协议头")]
		public string head { get; set; } = ""; //
		[CategoryAttribute("常规"), DescriptionAttribute("列数")]
		public int col_n { get; set; } = 0; //列数
		[CategoryAttribute("常规"), DescriptionAttribute("分隔符列表")]
		public string[] split_char_list { get; set; } = new string[0]; //
		public PD_LineObj_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("head")) head = v["head"] as string; //读取此行的首部名称
			if (v.ContainsKey("split_char_list")) //读取此行的自定义分隔符（先按默认分隔符确定协议，然后才按自定义的）
			{
				ArrayList l = v["split_char_list"] as ArrayList;
				split_char_list = new string[l.Count];
				for (int i = 0; i < l.Count; i++)
				{
					split_char_list[i] = l[i] as string;
				}
			}
			if (v.ContainsKey("col_n")) col_n = (int)v["col_n"];
		}
		public override bool display_2_var()
		{
			if (head != "") p_struct["head"] = head;
			p_struct["col_n"] = col_n;
			if (split_char_list.Length > 0)
			{
				var list = new ArrayList();
				foreach (var item in split_char_list)
				{
					list.Add(item);
				}
				p_struct["split_char_list"] = list;
			}
			return base.display_2_var();
		}
	}
	public class PD_Check_PropDis : PD_Obj_PropDis //重复协议域方式，与Obj域的区别在于仅第一个域有效，重复次数可配置可引用
	{
		[CategoryAttribute("常规"), DescriptionAttribute("校验模式")]
		public CheckMode mode { get; set; }= CheckMode.fix8; //
		[CategoryAttribute("常规"), DescriptionAttribute("字符类型，空为10进制，hex：16进制")]
		public string str_type { get; set; } = ""; //空为10进制，hex：16进制
		[CategoryAttribute("常规"), DescriptionAttribute("计算起始偏移")]
		public int st_pos { get; set; } = 0; //计算起始偏移
		[CategoryAttribute("常规"), DescriptionAttribute("固定数字符")]
		public string fix { get; set; } = ""; //默认是0
		public PD_Check_PropDis(string sn, ProtType t, JD v) : base(sn, t, v) //建立显示对象，输入结构描述对象名称（结构名）
		{
			if (v.ContainsKey("str_type")) str_type = v["str_type"] as string;
			if (v.ContainsKey("st_pos")) st_pos = (int)v["st_pos"];
			if (v.ContainsKey("mode"))
			{
				string s = Tool.json_ser.Serialize(v["mode"]); //这样取得的字符串带"
				mode = Tool.json_ser.Deserialize<CheckMode>(s); //取得参数类型，enum类型的反串行化需要字符串带"
			}
			if (v.ContainsKey("fix")) fix = v["fix"] as string;
		}
		public override bool display_2_var()
		{
			p_struct["mode"] = mode.ToString(); //如果直接用枚举，则串行化的时候出来是整数，而不是字符串
			if (str_type != "") p_struct["str_type"] = str_type;
			if (fix != "") p_struct["fix"] = fix;
			if (st_pos != 0) p_struct["st_pos"] = st_pos;
			return base.display_2_var();
		}
	}
#endregion
}
