using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WPF_Translator_Screen.Models;

namespace WPF_Translator_Screen.Services.Database
{
    // Services/SQLiteService.cs
    public class SQLiteService
    {
        private readonly string _dbPath;

        public SQLiteService()
        {
            // เก็บไฟล์ไว้ใน AppData ของเครื่อง user
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TranslateApp",
                "local.db"
            );

            // สร้าง folder ถ้าไม่มี
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

            InitializeDatabase();
        }

        // ── สร้าง Table ถ้ายังไม่มี ─────────────────────────────
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS pending_records (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                raw_input       TEXT    NOT NULL,
                translate_output TEXT   NOT NULL,
                source_language TEXT    NOT NULL,
                target_language TEXT    NOT NULL,
                app_source      TEXT    NOT NULL,
                context_name    TEXT,
                created_at      TEXT    NOT NULL,
                is_sent         INTEGER NOT NULL DEFAULT 0
            )";
            cmd.ExecuteNonQuery();
        }

        // ── Insert record ────────────────────────────────────────
        public void Insert(PendingRecord record)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
            INSERT INTO pending_records 
                (raw_input, translate_output, source_language, 
                 target_language, app_source, context_name, created_at)
            VALUES 
                ($rawInput, $translateOutput, $sourceLang,
                 $targetLang, $appSource, $contextName, $createdAt)";

            cmd.Parameters.AddWithValue("$rawInput", record.RawInput);
            cmd.Parameters.AddWithValue("$translateOutput", record.TranslateOutput);
            cmd.Parameters.AddWithValue("$sourceLang", record.SourceLanguage);
            cmd.Parameters.AddWithValue("$targetLang", record.TargetLanguage);
            cmd.Parameters.AddWithValue("$appSource", record.AppSource);
            cmd.Parameters.AddWithValue("$contextName", record.ContextName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$createdAt", record.CreatedAt.ToString("o"));

            cmd.ExecuteNonQuery();
        }

        // ── ดึง records ที่ยังไม่ได้ส่ง ─────────────────────────
        public List<PendingRecord> GetPending()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM pending_records WHERE is_sent = 0";

            var records = new List<PendingRecord>();
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                records.Add(new PendingRecord
                {
                    Id = reader.GetInt32(0),
                    RawInput = reader.GetString(1),
                    TranslateOutput = reader.GetString(2),
                    SourceLanguage = reader.GetString(3),
                    TargetLanguage = reader.GetString(4),
                    AppSource = reader.GetString(5),
                    ContextName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    IsSent = reader.GetInt32(8) == 1
                });
            }

            return records;
        }

        // ── Mark ว่าส่งแล้ว (หลัง API ตอบ 200) ─────────────────
        public void MarkAsSent(List<int> ids)
        {
            if (ids.Count == 0) return;

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
            UPDATE pending_records 
            SET is_sent = 1 
            WHERE id IN ({string.Join(",", ids)})";

            cmd.ExecuteNonQuery();
        }

        // ── ลบ records ที่ส่งแล้ว (cleanup) ─────────────────────
        public void DeleteSent()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM pending_records WHERE is_sent = 1";
            cmd.ExecuteNonQuery();
        }

        // ── นับ pending ที่รอส่ง ─────────────────────────────────
        public int CountPending()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pending_records WHERE is_sent = 0";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }
}
