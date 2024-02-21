using CommandDotNet;
using System.Configuration;
using System.Data.SqlClient;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Transactions;

namespace SQLVersionConsoleApp
{
    public class Program
    {
        public static string connectionString = string.Empty;

        public Program()
        {
            connectionString = new(ConfigurationManager.ConnectionStrings["MISConnectionString"].ToString());
        }

        static int Main(string[] args)
        {
            return new AppRunner<Program>().Run(args);
        }

        [Command(Description = "建立 DBVersion Table")]
        public void CreateVersionTable()
        {
            string sql = @"CREATE TABLE [dbo].[DBVersion](
	                        [DBVersionID] [int] IDENTITY(1,1) NOT NULL,
	                        [Version] [nchar](10) NULL,
                            [Comment] [nvarchar](255) NULL,
	                        [PatchTime] [datetime] NULL,
                         CONSTRAINT [PK_DBVersion] PRIMARY KEY CLUSTERED
                        (
	                        [DBVersionID] ASC
                        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                        ) ON [PRIMARY]
                        GO

                        ALTER TABLE [dbo].[DBVersion] ADD  CONSTRAINT [DF_DBVersion_PatchTime]  DEFAULT (getdate()) FOR [PatchTime]
                        GO

                        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'版本' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DBVersion', @level2type=N'COLUMN',@level2name=N'Version'
                        GO

                        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'註解' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DBVersion', @level2type=N'COLUMN',@level2name=N'Comment'
                        GO

                        EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'更新時間' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'DBVersion', @level2type=N'COLUMN',@level2name=N'PatchTime'
                        GO";

            if(IsExecuteSuccess(sql))
            {
                Console.WriteLine("Execute Success");
            }
        }

        [Command(Description = "更新 DB")]
        public void Patch([Operand(Description = "SQL檔案路徑:C:\\myTest\\DBpatchs\\patch.sql")] string sqlFilePath)
        {
            string sqlContent = File.ReadAllText(sqlFilePath);

            List<VersionSqlPair> versionSqlPairs = ExtractVersionSqlPairs(sqlContent);

            string currentVersion = GetCurrentVersion();

            if (string.IsNullOrEmpty(currentVersion))
            {
                currentVersion = "0";
            }

            Console.WriteLine($"currentVersion Version: {currentVersion}");
            Console.WriteLine("Execute Start");
            foreach (var pair in versionSqlPairs)
            {
                if (pair.CompareVersions(currentVersion) > 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"Run Version: {pair.Version}");

                    if (!IsExecuteSuccess(pair.Sql))
                    {
                        break;
                    }

                    Console.WriteLine("===========================================");
                    Console.WriteLine("");
                    Console.WriteLine("Execute Success");
                    Console.WriteLine("");

                    currentVersion = pair.Version;
                    UpdateCurrentVersion(pair);
                }
            }
            Console.WriteLine("Execute End");
        }

        private static string GetCurrentVersion()
        {
            string currentVersion = string.Empty;
            using SqlConnection connection = new(connectionString);
            connection.Open();
            using SqlCommand command = new("SELECT TOP 1 Version FROM DBVersion ORDER BY DBVersionID DESC", connection);
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                currentVersion = reader.GetString(0);
            }

            return currentVersion;
        }

        private static void UpdateCurrentVersion(VersionSqlPair versionSqlPair)
        {
            using SqlConnection connection = new(connectionString);
            connection.Open();
            using SqlCommand command = new($"INSERT INTO DBVersion (Version, Comment) VALUES ('{versionSqlPair.Version}', '{versionSqlPair.Comment}')", connection);
            command.ExecuteNonQuery();
        }

        public static List<VersionSqlPair> ExtractVersionSqlPairs(string sqlContent)
        {
            List<VersionSqlPair> versionSqlPairs = [];
            string pattern = @"###NEW_VERSION### Version=((\.?\d*)+)[\s\S]+?(###COMMENT###([\s\S]+?))?###SQL###([\s\S]+?)###END###";
            MatchCollection matches = Regex.Matches(sqlContent, pattern);

            matches.Where(m => m.Groups.Count == 6).ToList().ForEach(m =>
            {
                versionSqlPairs.Add(
                    new VersionSqlPair
                    {
                        Version = m.Groups[1].Value,
                        Comment = m.Groups[4].Value.Trim(),
                        Sql = m.Groups[5].Value.Trim()
                    }
                 );
            });

            return versionSqlPairs;
        }

        private static bool IsExecuteSuccess(string sql)
        {
            bool result = true;
            using (SqlConnection connection = new (ConfigurationManager.ConnectionStrings["MISConnectionString"].ToString()))
            {
                connection.Open();
                using (SqlTransaction tr = connection.BeginTransaction())
                {
                    try
                    {
                        sql.Split(new string[] { "GO" }, StringSplitOptions.RemoveEmptyEntries).ToList().ForEach(sql =>
                        {
                            Console.WriteLine("===========================================");
                            Console.WriteLine(sql);
                            using SqlCommand command = new(sql, connection, tr);
                            command.ExecuteNonQuery();
                        });

                        tr.Commit();
                    }
                    catch (Exception ex)
                    {
                        tr.Rollback();
                        result = false;
                        Console.WriteLine("===========================================");
                        Console.WriteLine("Execute fail !!!?");
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            return result;
        }
    }

    public class VersionSqlPair
    {
        public string Version { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string Sql { get; set; } = string.Empty;

        public int CompareVersions(string version)
        {
            int[] v1 = Version.Split('.').Select(int.Parse).ToArray();
            int[] v2 = version.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Min(v1.Length, v2.Length); i++)
            {
                if (v1[i] < v2[i])
                {
                    return -1;
                }
                else if (v1[i] > v2[i])
                {
                    return 1;
                }
            }
            return v1.Length.CompareTo(v2.Length);
        }
    }
}
