using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Dapper;
using Dapper.Contrib.Extensions;
using RT.Util;

namespace StatusScreenSite
{
    class TbSchema
    {
        [ExplicitKey]
        public string ServiceName { get; set; }
        public int SchemaVersion { get; set; }
    }

    class Db
    {
        private static string _dbFilePath;

        public static void Initialise(string dbFilePath, IEnumerable<IService> services)
        {
            _dbFilePath = dbFilePath;

            SqlMapperExtensions.TableNameMapper = type => type.Name;

            if (!File.Exists(_dbFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_dbFilePath));
                SQLiteConnection.CreateFile(_dbFilePath);
                using (var db = new SQLiteConnection($"Data Source={_dbFilePath};Version=3;").OpenAndReturn())
                {
                    db.Execute($@"CREATE TABLE {nameof(TbSchema)} (
                        {nameof(TbSchema.ServiceName)} TEXT NOT NULL PRIMARY KEY,
                        {nameof(TbSchema.SchemaVersion)} INT NOT NULL
                    )");
                }
            }

            foreach (var service in services)
                migrateServiceSchema(service);
        }

        private static void migrateServiceSchema(IService service)
        {
            int curVersion;
            using (var db = Db.Open())
                curVersion = db.Get<TbSchema>(service.ServiceName)?.SchemaVersion ?? 0;

            while (true)
            {
                var tempName = Util.GetNonexistentFileName(n => $"{_dbFilePath}.~{n}");
                Ut.WaitSharingVio(() => File.Copy(_dbFilePath, tempName));

                bool changesMade = false;
                using (var db = new SQLiteConnection($"Data Source={tempName};Version=3;").OpenAndReturn())
                {
                    changesMade = service.MigrateSchema(db, curVersion);
                    if (changesMade)
                    {
                        curVersion++;
                        var row = new TbSchema { ServiceName = service.ServiceName, SchemaVersion = curVersion };
                        if (curVersion == 1)
                            db.Insert(row);
                        else
                            db.Update<TbSchema>(row);
                    }
                }

                if (!changesMade)
                {
                    Ut.WaitSharingVio(() => File.Delete(tempName));
                    return;
                }

                Ut.WaitSharingVio(() => File.Delete(_dbFilePath));
                Ut.WaitSharingVio(() => File.Move(tempName, _dbFilePath));
            }
        }

        public static SQLiteConnection Open()
        {
            return new SQLiteConnection($"Data Source={_dbFilePath};Version=3;").OpenAndReturn();
        }
    }
}
