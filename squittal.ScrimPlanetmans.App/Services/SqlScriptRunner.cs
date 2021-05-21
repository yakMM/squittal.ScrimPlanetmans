﻿using System;
using System.IO;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;

namespace squittal.ScrimPlanetmans.Services
{
    public class SqlScriptRunner : ISqlScriptRunner
    {
        private readonly string _sqlDirectory = "Data\\SQL";
        private readonly string _basePath;
        private readonly string _scriptDirectory;
        private readonly string _adhocScriptDirectory;

        private readonly Server _server;

        private readonly ILogger<SqlScriptRunner> _logger;

        public SqlScriptRunner(ILogger<SqlScriptRunner> logger, IConfiguration configuration, IWebHostEnvironment env)
        {
            _logger = logger;
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(configuration.GetConnectionString("PlanetmansDbContext"));

            _server = new Server(builder.DataSource);

            _basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
            _scriptDirectory = Path.Combine(_basePath, _sqlDirectory);

            _adhocScriptDirectory = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..\\sql_adhoc"));
        }

        public void RunSqlScript(string fileName, bool minimalLogging = false)
        {
            var scriptPath = Path.Combine(_scriptDirectory, fileName);
            
            try
            {
                var scriptFileInfo = new FileInfo(scriptPath);

                string scriptText = scriptFileInfo.OpenText().ReadToEnd();
                
                _server.ConnectionContext.ExecuteNonQuery(scriptText);

                if (!minimalLogging)
                {
                    _logger.LogInformation($"Successfully ran sql script at {scriptPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error running sql script {scriptPath}: {ex}");
            }
        }

        public bool TryRunAdHocSqlScript(string fileName, out string info, bool minimalLogging = false)
        {
            var scriptPath = Path.Combine(_adhocScriptDirectory, fileName);

            try
            {
                var scriptFileInfo = new FileInfo(scriptPath);

                string scriptText = scriptFileInfo.OpenText().ReadToEnd();

                _server.ConnectionContext.ExecuteNonQuery(scriptText);

                info = $"Successfully ran sql script at {scriptPath}";

                if (!minimalLogging)
                {
                    _logger.LogInformation(info);
                }

                return true;
            }
            catch (Exception ex)
            {
                info = $"Error running sql script {scriptPath}: {ex}";

                _logger.LogError(info);

                return false;
            }
        }

        public void RunSqlDirectoryScripts(string directoryName)
        {
            var directoryPath = Path.Combine(_scriptDirectory, directoryName);

            try
            {
                var files = Directory.GetFiles(directoryPath);

                foreach (var file in files)
                {
                    if (!file.EndsWith(".sql"))
                    {
                        continue;
                    }

                    RunSqlScript(file, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error running SQL scripts in directory {directoryName}: {ex}");
            }
        }
    }
}
