using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace lgd_gui
{
    /// <summary>
    /// help.xaml 的交互逻辑
    /// </summary>
    public partial class Dlg_help : Window
    {
        public Dlg_help()
        {
            InitializeComponent();
        }
		public string helptext="";
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			LoadText(helptext);
		}
		private void LoadText(string txtContent)
		{
			rtb_help.Document.Blocks.Clear();
			Paragraph paragraph = new Paragraph();
			paragraph.Inlines.Add(txtContent);
			rtb_help.Document.Blocks.Add(paragraph);
		}
	}
}
