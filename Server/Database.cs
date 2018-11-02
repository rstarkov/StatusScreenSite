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
            SqlMapperExtensions.TableNameMapper = type => type.Name;

            if (!File.Exists(dbFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dbFilePath));
                SQLiteConnection.CreateFile(dbFilePath);
                using (var db = new SQLiteConnection($"Data Source={dbFilePath};Version=3;").OpenAndReturn())
                {
                    db.Execute($@"CREATE TABLE {nameof(TbSchema)} (
                        {nameof(TbSchema.ServiceName)} TEXT NOT NULL PRIMARY KEY,
                        {nameof(TbSchema.SchemaVersion)} INT NOT NULL
                    )");
                }
            }

            foreach (var service in services)
                migrateServiceSchema(service, dbFilePath);

            // Set this last so that it's not possible to accidentally use the DB before it's fully initialised
            _dbFilePath = dbFilePath;
        }

        private static void migrateServiceSchema(IService service, string dbFilePath)
        {
            int curVersion;
            using (var db = new SQLiteConnection($"Data Source={dbFilePath};Version=3;").OpenAndReturn())
                curVersion = db.Get<TbSchema>(service.ServiceName)?.SchemaVersion ?? 0;

            while (service.MigrateSchema(null, curVersion))
            {
                var tempName = Util.GetNonexistentFileName(n => $"{dbFilePath}.~{n}");
                Ut.WaitSharingVio(() => File.Copy(dbFilePath, tempName));

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

                Ut.WaitSharingVio(() => File.Delete(dbFilePath));
                Ut.WaitSharingVio(() => File.Move(tempName, dbFilePath));
            }
        }

        public static SQLiteConnection Open()
        {
            return new SQLiteConnection($"Data Source={_dbFilePath};Version=3;").OpenAndReturn();
        }
    }
}
