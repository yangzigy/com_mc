using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cslib;
namespace com_mc
{
	static public class Log_Tools //日志数据文件的工具功能
	{
#region cmlog修改基准时间戳
		static public void fun_cmlog_time() //cmlog修改基准时间戳
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.cmlog|*.cmlog";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName)+"/"+ Path.GetFileNameWithoutExtension(fin)+"_m.cmlog";
			
			Com_Dlg cdlg = new Com_Dlg(); //获取输入的对话框
			//给对话框添加内容：
			Label lb = new Label(); //
			lb.Content = "输入起始时间（ms）";
			lb.FontSize = 14;
			lb.VerticalAlignment = VerticalAlignment.Center;
			TextBox tb = new TextBox();
			tb.Text = "0";
			cdlg.mgrid.Children.Add(lb);
			Grid.SetColumn(lb, 0);
			Grid.SetRow(lb, 0);
			cdlg.mgrid.Children.Add(tb);
			Grid.SetColumn(tb, 1);
			Grid.SetRow(tb, 0);

			if (cdlg.ShowDialog() == false) return;
			int stms = 0;
			bool r=int.TryParse(tb.Text,out stms);
			if(r==false || stms<0)
			{
				MessageBox.Show("输入非正整数");
				return;
			}
			//拿到了输入输出文件，起始时间
			FileStream fs = new FileStream(fin, FileMode.Open, FileAccess.Read);
			byte[] org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();
			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);
			//开始转换
			int first_ms = -1;
			for (int i = 0; i < org_data.Length - 6;) //将内存中的数据添加到行列表
			{
				CMLOG_HEAD h = (CMLOG_HEAD)Tool.BytesToStruct(org_data, i, typeof(CMLOG_HEAD));
				int len = h.len; //len这个域是长度
				int ms = h.ms;
				if (first_ms < 0) first_ms = ms; //第一个ms值
				i += Marshal.SizeOf(h);

				h.ms = h.ms - first_ms + stms;

				var temp = Tool.StructToBytes(h);
				fsout.Write(temp,0, temp.Length); //写入头部
				fsout.Write(org_data, i, len); //写入数据
				i += len;
			}
			fsout.Close();
		}
#endregion
#region hex原始数据转二进制
		static public bool ishex(char c) //判断输入是否是hex字符(只需判断小写字母即可)
		{
			if (char.IsDigit(c)) return true;
			if (c >= 'a' && c <= 'f') return true;
			return false;
		}
		static public void fun_hex2bin() //hex原始数据转二进制
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" +
							Path.GetFileNameWithoutExtension(fin) + "_m"+
							Path.GetExtension(fin);
			//打开输入文件
			StreamReader sw = new StreamReader(fin);
			string text = sw.ReadToEnd(); //输入数据可以不分行
			sw.Close();
			//打开输出文件
			FileStream fsout = new FileStream(fout, FileMode.Create, FileAccess.Write);

			text =text.Replace("0x", " ").ToLower(); //都转换成小写的
			int stat = 0;//状态，0：找字符；1：找第二个字符；
			int st = 0; //起始字符
			byte[] b = new byte[4]; //用于输出的字节数组
			for (int i = 0; i < text.Length; i++)
			{
				string s = "";
				if(stat==0) //若是找第一个字符
				{
					if (ishex(text[i])) //若是hex
					{
						st = i;
						stat = 1;
					}//若不是hex字符，都算空白字符，略过
				}
				else if(stat==1) //若是找第二个字符
				{
					if (ishex(text[i])) //若是hex
					{
						s = text.Substring(st, 2);
					}
					else //若不是hex字符，都算空白字符，就处理单个字符
					{
						s = text.Substring(st, 1);
					}
					stat = 0;
				}
				if(s!="") //若找到了一个hex字节
				{
					b[0] = byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
					fsout.Write(b,0,1);
				}
			}
			if (stat == 1) //若最后一个是单个字符，也要输出
			{
				b[0] = byte.Parse(text.Substring(st, 1), System.Globalization.NumberStyles.HexNumber);
				fsout.Write(b, 0, 1);
			}
			fsout.Close();
		}
		static public string[] bin2text_type_tab = new string[] //转换类型的名称
		{
			"u8","u16","u32","u64","s8","s16","s32","s64",
			"hex8","hex16","hex32","hex64","float","dobule"
		};
		static public uint[] bin2text_type_len = new uint[] //转换类型的长度
		{
			1,2,4,8,1,2,4,8,1,2,4,8,4,4
		};
#endregion
#region 二进制原始数据转文本
		static public void fun_bin2text() //二进制原始数据转文本
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" +
							Path.GetFileNameWithoutExtension(fin) + "_m.txt";

			Com_Dlg cdlg = new Com_Dlg(); //获取输入的对话框
			Grid subgrid = new Grid(); //控件容器
			cdlg.mgrid.Children.Add(subgrid);
			Grid.SetColumn(subgrid, 0);
			Grid.SetRow(subgrid, 0);
			Grid.SetColumnSpan(subgrid, 2);
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.RowDefinitions.Add(new RowDefinition());
			subgrid.ColumnDefinitions.Add(new ColumnDefinition());
			subgrid.ColumnDefinitions.Add(new ColumnDefinition());
			cdlg.Height = 260;
			//第一列：描述文本
			Label lb_col = new Label();	lb_col.Content = "列数";
			subgrid.Children.Add(lb_col);
			Grid.SetRow(lb_col, 0); Grid.SetColumn(lb_col,0);

			Label lb_type = new Label();	lb_type.Content = "类型";
			subgrid.Children.Add(lb_type);
			Grid.SetRow(lb_type, 1); Grid.SetColumn(lb_type, 0);

			Label lb_st = new Label(); lb_st.Content = "起始(B)";
			subgrid.Children.Add(lb_st);
			Grid.SetRow(lb_st, 2); Grid.SetColumn(lb_st, 0);

			Label lb_rowdis = new Label(); lb_rowdis.Content = "行间距(0为列宽)";
			subgrid.Children.Add(lb_rowdis);
			Grid.SetRow(lb_rowdis, 3); Grid.SetColumn(lb_rowdis, 0);

			Label lb_row = new Label(); lb_row.Content = "行数(0为至尾)";
			subgrid.Children.Add(lb_row);
			Grid.SetRow(lb_row, 4); Grid.SetColumn(lb_row, 0);
			//第二列：输入数值
			TextBox tb_col = new TextBox(); tb_col.Text = "8";
			subgrid.Children.Add(tb_col);
			Grid.SetRow(tb_col, 0); Grid.SetColumn(tb_col, 1);

			ComboBox cb_type = new ComboBox(); cb_type.VerticalContentAlignment = VerticalAlignment.Center;
			for (int i = 0; i < bin2text_type_tab.Length; i++)
			{
				cb_type.Items.Add(bin2text_type_tab[i]);
			}
			cb_type.SelectedIndex = 8; //默认为hex8
			subgrid.Children.Add(cb_type);
			Grid.SetRow(cb_type, 1); Grid.SetColumn(cb_type, 1);

			TextBox tb_st = new TextBox(); tb_st.Text = "0";
			subgrid.Children.Add(tb_st);
			Grid.SetRow(tb_st, 2); Grid.SetColumn(tb_st, 1);

			TextBox tb_rowdis = new TextBox(); tb_rowdis.Text = "0";
			subgrid.Children.Add(tb_rowdis);
			Grid.SetRow(tb_rowdis, 3); Grid.SetColumn(tb_rowdis, 1);

			TextBox tb_row = new TextBox(); tb_row.Text = "0";
			subgrid.Children.Add(tb_row);
			Grid.SetRow(tb_row, 4); Grid.SetColumn(tb_row, 1);

			//弹出对话框，输入
			if (cdlg.ShowDialog() == false) return;

			//打开输入文件
			FileStream fs = new FileStream(fin, FileMode.Open, FileAccess.Read);
			byte[] org_data = new byte[fs.Length];
			fs.Read(org_data, 0, org_data.Length);
			fs.Close();

			//读取输入值
			uint cols = 0; //列数
			int type = cb_type.SelectedIndex; //类型选择直接用序号
			uint st = 0; //起始偏移
			uint row_dis = 0; //行间隔
			uint rows = 0; //行数
			if ((!uint.TryParse(tb_col.Text,out cols)) || cols==0 ||
				(!uint.TryParse(tb_st.Text, out st)) || st>=org_data.Length ||
				(!uint.TryParse(tb_rowdis.Text, out row_dis)) || (!uint.TryParse(tb_row.Text, out rows)))
			{
				MessageBox.Show("参数错误");
				return;
			}
			uint type_len = bin2text_type_len[type]; //类型长度（列元素长度）
			uint w=cols * type_len; //数据列宽
			if (row_dis == 0) row_dis = w; //若指定的行间距为0，行间距为列宽
			uint l_ava = (uint)(org_data.Length - st); //有效数据量
			uint row_ava = (l_ava + row_dis - 1) / row_dis; //有效的行数
			if (rows == 0) rows = row_ava; //如果指定的长度为0，至尾
			else if (rows > row_ava) //若指定了长度但不对
			{
				MessageBox.Show("行数超过有效数据");
				return;
			}
			if(row_dis<w)
			{
				MessageBox.Show("行间距小于列宽");
				return;
			}
			int half_line = (int)(rows * row_dis - l_ava); //若最后一行数据不够
			if (half_line>0 && //输出行数*行间距大于有效数据，说明有半行
				half_line < cols*type_len && //且这半行数据的截止在有效数据以内
				(half_line % type_len)!=0) //半行数据不是元素长度的整数倍
			{
				MessageBox.Show("数据不是类型的整数倍");
				return;
			}
			//打开输出文件
			StreamWriter sw = new StreamWriter(fout);
			uint end = l_ava + (uint)st;
			string s = "";
			int ind = (int)st; //当前偏移
			for (int row = 0; row < rows; row++)
			{
				for (int col = 0; col < cols && ind<org_data.Length; col++)
				{
					switch (type)
					{
						case 0: s += string.Format("{0} ", org_data[ind]); break; //u8
						case 1: s += string.Format("{0} ", (UInt16)Tool.BytesToStruct(org_data, ind, typeof(UInt16))); break; //u16
						case 2: s += string.Format("{0} ", (UInt32)Tool.BytesToStruct(org_data, ind, typeof(UInt32))); break; //u32
						case 3: s += string.Format("{0} ", (UInt64)Tool.BytesToStruct(org_data, ind, typeof(UInt64))); break; //u64
						case 4: s += string.Format("{0} ", (sbyte)org_data[ind]); break; //s8
						case 5: s += string.Format("{0} ", (Int16)Tool.BytesToStruct(org_data, ind, typeof(Int16))); break; //s16
						case 6: s += string.Format("{0} ", (Int32)Tool.BytesToStruct(org_data, ind, typeof(Int32))); break; //s32
						case 7: s += string.Format("{0} ", (Int64)Tool.BytesToStruct(org_data, ind, typeof(Int64))); break; //s64
						case 8: s += string.Format("{0:X02} ", org_data[ind]); break; //hex8
						case 9: s += string.Format("{0:X04} ", (UInt16)Tool.BytesToStruct(org_data, ind, typeof(UInt16))); break; //hex16
						case 10: s += string.Format("{0:X08} ", (UInt32)Tool.BytesToStruct(org_data, ind, typeof(UInt32))); break; //hex32
						case 11: s += string.Format("{0:X016} ", (UInt64)Tool.BytesToStruct(org_data, ind, typeof(UInt64))); break; //hex64
						case 12: s += string.Format("{0} ", (float)Tool.BytesToStruct(org_data, ind, typeof(float))); break; //float
						case 13: s += string.Format("{0} ", (double)Tool.BytesToStruct(org_data, ind, typeof(double))); break; //double
						default: continue;
					}
					ind+=(int)type_len;
				}
				ind += (int)(row_dis-w); //行结束，跳到下一行起始
				sw.WriteLine(s);
				s = "";
			}
			sw.Close();
		}
#endregion
#region 原始数据转cmlog
		static public void fun_org2cmlog() //原始数据转cmlog
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.*|*.*";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" + Path.GetFileNameWithoutExtension(fin) + "_m.cmlog";
		}
#endregion
#region cmlog文件合并
		static public void fun_merge_cmlog() //cmlog文件合并
		{
			var ofd = new System.Windows.Forms.OpenFileDialog();
			ofd.Filter = "*.cmlog|*.cmlog";
			if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) throw new Exception("未选择文件");
			string fin = ofd.FileName;
			string fout = Path.GetDirectoryName(ofd.FileName) + "/" + Path.GetFileNameWithoutExtension(fin) + "_m.cmlog";
		}
#endregion
	}
}
