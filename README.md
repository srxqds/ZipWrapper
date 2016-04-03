# ZipWrapper
A simple zip wrapper based on Lzma and Zlib compress algorithm using in Unity3D!

处于几个原因或目的：

  1.去年用Unity4.6将我们游戏移植到iOS 64位的时候，首先遇到的是我们压缩工具库 ISharpCode.SharpZiplib.dll 不能被il2cpp支持（现在不知道有没有支持），当时在gitbub上找了UnityZip(其实就是用的Ionic.Zip)。

  2.这个项目移植到 Window Phone 上面也遇到不支持的情况。
  
  3.对压缩算法还是只停留在以前课堂学到haffman编码，一堆名词（GZip,Deflate,Zip,Lzma等）一直没搞明白之间的关系和区别，所以一直都很想更多了解一下。
  
  4.Zip Archieve 在.Net framework 4.5才支持，虽然今天还看到Unity已经加入.Net基金会（肯定会有更多的支持），但是像Zlib 和 Lzma 和 Zip 几乎已经没有太多变化了，为了后面可以有更多的自由度所以干脆自己折腾了！

Todo
  1.学好英语
  
  2.对代码进行整理
  
  3.对异步，多线程和Unity更好的结合
  

Thanks
  1.NotNetZip:http://dotnetzip.codeplex.com/
  2.ZipStorer:https://github.com/jaime-olivares/zipstorer and https://github.com/neremin/ZipStorerTest


2016 Guangzhou, Lucky Game
D.S. Qiu
