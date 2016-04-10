# ZipWrapper
A simple zip archive wrapper use Lzma and Zlib compress algorithm for Unity3D!


出于几个原因或目的：

  1.去年用Unity4.6将我们游戏移植到iOS 64位的时候，首先遇到的是我们压缩工具库 ISharpCode.SharpZiplib.dll 不能被il2cpp支持（现在不知道有没有支持），当时在gitbub上找了UnityZip(https://github.com/tsubaki/UnityZip)。

  2.这个项目移植到 Window Phone 上面也遇到不支持的情况。
  
  3.对压缩算法还是只停留在以前课堂学到haffman编码，一堆名词（GZip,Deflate,Zip,Lzma等）一直没搞明白之间的关系和区别，所以一直都很想更多了解一下。
  
  4.Zip Archieve 在.Net framework 4.5才支持，虽然今天还看到Unity已经加入.Net基金会（肯定会有更多的支持），但是像Zlib 和 Lzma 和 Zip 几乎已经没有太多变化了，为了后面可以有更多的自由度所以干脆自己折腾了！
  
  5.已有的库要么收费（ZipForge.NET,也有zlib.net开源——一开始我就用这个来封装结果跪了，看了ZInputStream真心敷衍）要么就是太庞大了没必要（SharpZip和Ionci.Zip，其实也还好）。

Todo
  
  1.学好英语
  
  2.对代码进行整理
  
  3.对异步，多线程和Unity更好的结合
  

Thanks
  
  1.NotNetZip:http://dotnetzip.codeplex.com/
  
  2.Lzma:http://www.7-zip.org/sdk.html
  
  2.ZipStorer:https://github.com/jaime-olivares/zipstorer and https://github.com/neremin/ZipStorerTest


D.S. Qiu

2016 Guangzhou, Lucky Game


