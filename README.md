# LoggerCSharp
日志存储库
可在HKEY_LOCAL_MACHINE\SOFTWARE\LoggerCSharp配置日志要不要写入
# 如：
配置HKEY_LOCAL_MACHINE\SOFTWARE\LoggerCSharp下的testlog = Error，那么你的testlog project将只会记录类型为Error的log
删除注册表键值testlog后，则不会进行日志的记录，可在项目时，随时修改或者删除该键值
日志默认生成在，应用程序根目录
