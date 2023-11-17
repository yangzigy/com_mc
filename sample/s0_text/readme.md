<link rel="stylesheet" type="text/css" href="../../doc/base.css">

s0_text 最简案例：纯文本协议  
======  
## 协议
默认配置的协议，纯文本形式，设备信息通过多行文本上传，上位机指令通过文本行下发。共有3种行结构：
上传协议（共8列）：  
| 0 | 1  | 2 | 3 | 4 | 5 | 6 | 7 |
| -- | --  | -- | -- | -- | -- | -- | -- |  
| 电压 | 空 | 电流 | 强度 | 期望温度 | 温度 | 空 | 状态字 |  
| 30.5 | 518 | 0.3 | 0.0 | 24.6 | 24.57 | 315.07 | 208DB9 |  
其中状态字为hex字符，表示一个4字节整数:  
第9~10bit代表工作模式，
第6bit代表调制输出  
第15bit代表温控  
参数协议（共2列）：  
| 参数 | 0列 | 1列  |  
| -- | --  | -- |  
| 指令结果 | $r | err或OK |  
| 电流偏置 | $@:3:1:0:16 | 数值 |  
## 配置
配置网络端口：  
```json
{
	"type":"udp", //数据源类型：udp
	"name":"udp1",
	"ip" : "127.0.0.1", "port" : 12345,
	"rmt_ip" : "127.0.0.1", "rmt_port" : 12346
}
```  
## 测试
测试数据为2021-12-09_21.ttlog，以及“原始数据.org”
