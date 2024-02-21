設定DB連線
SQLVersionConsoleApp\bin\Debug\net8.0\SQLVersionConsoleApp.dll.config
<add name="MISConnectionString" connectionString="Data Source=192.168.xxx.xxx,1466;Initial Catalog=MeetingManagement;User ID=xxx;password=xxxxxx"/>

建立版控Table
SQLVersionConsoleApp\bin\Debug\net8.0>SQLVersionConsoleApp CreateVersionTable

執行 Patch
SQLVersionConsoleApp\bin\Debug\net8.0>SQLVersionConsoleApp Patch C:\xxx\xxx\xxxx.sql
