using MongoDB.Bson;
using MongoDB.Driver;
using QCGO.Models;
using Microsoft.Extensions.Logging;

namespace QCGO.Services
{
    public class AccountService
    {
        private readonly IMongoCollection<Account>? _accounts;
        private readonly ILogger<AccountService> _logger;

        public AccountService(MongoSettings settings, ILogger<AccountService> logger)
        {
            _logger = logger;
            try
            {
                var client = new MongoClient(settings.ConnectionString);
                var db = client.GetDatabase(settings.DatabaseName ?? "QCGO");
                _accounts = db.GetCollection<Account>("accounts");
                db.RunCommandAsync((Command<BsonDocument>)"{ping:1}").GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AccountService");
                _accounts = null;
            }
        }

        public Account? FindByUsername(string username)
        {
            if (_accounts == null) return null;
            try
            {
                var filter = Builders<Account>.Filter.Eq(a => a.Username, username);
                return _accounts.Find(filter).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying accounts collection");
                return null;
            }
        }

        public bool ValidateCredentials(string username, string password)
        {
            var acct = FindByUsername(username);
            if (acct == null) return false;
            // NOTE: currently comparing plaintext. Replace with secure hash check in production.
            return acct.Password == password;
        }

        public bool Exists(string username)
        {
            return FindByUsername(username) != null;
        }

        public bool CreateAccount(string username, string password)
        {
            if (_accounts == null) return false;
            try
            {
                var acct = new Account { Username = username, Password = password };
                _accounts.InsertOne(acct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create account");
                return false;
            }
        }
    }
}
