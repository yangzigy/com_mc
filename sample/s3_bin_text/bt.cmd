{
	"cmds":
	[
		{"name":"开数据","cmd":"s 1\n^x_axis","type":"bt"},
		{"name":"关数据","cmd":"s 0","type":"bt"},
		{"name":"显示原始","cmd":"oa 1\ns 0\n^clear\n^x_axis 采样值","type":"bt"},
		{"name":"温度期望","cmd":"st","type":"bt","suffixname":"tb_exp_T","repeat_T":5},
		{"name":"tb_exp_T","type":"text","dft":"25"},
		{"name":"0~35℃","type":"label"},
		{"name":"直流电流","cmd":"si","type":"bt","suffixname":"tb_i"},
		{"name":"tb_i","type":"para","dft":"80","cmd":"@ 0,1,0,16,0","refdname":"i_off"},
		{"name":"直流偏移","cmd":"i_off","type":"bt","suffixname":"tb_i"},
		{"name":"标0","refdname":"指令结果","cmd":"cali 0","type":"rpl_bool"},
		{"name":"","type":"label"},
		{"name":"","type":"label"},
		{"name":"温控","refdname":"温控","dft":"开",
			"cmd":"outc 1","cmdoff":"outc 0","type":"sw","c_span":2},
		{"name":"输出","refdname":"输出","dft":"开",
			"cmd":"isw 1","cmdoff":"isw 0","type":"sw","c_span":2}
	]
}
