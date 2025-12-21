
using Indx.Api;
using Indx.CloudApi;
using Indx.Storage;
namespace IndxCloudApi.Models
{
    internal sealed class IndxCloudInternalApi
    {
        #region Public Methods
        public SearchEngine FindSearchEngineForInit(string dataSetName, string userId)
        {
            var matcher = FindInstance(dataSetName, userId, true);
            if (matcher.Status.SystemState == SystemState.Created)
                return matcher;
            matcher.Dispose();
            _instances.Remove(MakeKey(dataSetName, userId));
            var persistence = new Persistence(IndxCloudInternalApi.SearchDbConnectionString, dataSetName, userId);
            // invariant; DataSetExists() == true   
            int? configuration = persistence.ReadDataSetConfiguration();
            persistence.CreateOrOpenDataSet((int)configuration);
            matcher = new SearchEngine(MakeLogPrefix(userId, dataSetName), Indx.Utilities.ILoggerFactory.GetFactory(logFileName),
               (int)configuration, "")
            {
                Persistence = persistence
            };
            _instances.Add(MakeKey(dataSetName, userId), new SearchEngineInstance() { theInstance = matcher });
            return matcher;
        }
        public SearchEngine FindSearchEngine(string dataSetName, string userId, bool forceCreate = false)
        {
            return FindInstance(dataSetName, userId, forceCreate);
        }
        #endregion Public Methods

        #region Internal Fields
        internal const string logFileName = "IndxCloudApi.log";
        #endregion Internal Fields

        #region Internal Properties
        // API will not get used before after program.cs has executed App.Run. It will however, call
        // IndxCloudInternalAPI.StartUpSystem first, ensuring Manager cannot be null.
        internal static IndxCloudInternalApi Manager { private set; get; }

        #endregion Internal Properties

        #region Internal Methods
        internal static void StartUpSystem(string dbConnectionString)
        {
            if (Manager != null)
            {
                throw new System.InvalidOperationException("IndxCloudInternalAPI.Startupsystem shall only be called once");
            }
            Manager = new IndxCloudInternalApi(dbConnectionString);
            Manager.InitializeSystem();
        }
        internal static string SearchDbConnectionString { get; private set; }
        /// <summary>
        /// After Loading, insertions and deletions call this function
        /// to perform the actual indexing. Use the GetState method
        /// to monitor progress and readiness for Search.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        internal bool DoIndex(string dataSetName, string userId)
        {
            try
            {
                var pm = new ProcessMonitor();
                var engine = FindInstance(dataSetName, userId, true);
                if (engine != null && (engine.Status.SystemState == SystemState.Loaded
                    || engine.Status.SystemState == SystemState.Ready))
                {
                    engine.Index(pm);
                    pm.WaitForCompletion();
                    return true;
                }
                else
                    return false;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(MakeLogPrefix(userId, dataSetName) + "IndxCloudInternalApi.DoIndexAsync exception" + ex.ToString());
                throw;
            }
        }

        internal string[] GetFields(string dataSetName, string userId, bool all, bool indexable, bool sortable, bool filterable, bool facetable)
        {
            try
            {
                var engine = FindInstance(dataSetName, userId, true);
                if (engine == null)
                    return new string[0];
                {
                    var fields = engine.GetFieldList();
                    var returnList = new List<string>(fields.Count);
                    for (int i = 0; i < fields.Count; i++)
                        if (all)
                            returnList.Add(fields[i].Name);
                        else if (fields[i].Searchable && indexable)
                            returnList.Add(fields[i].Name);
                        else if (fields[i].Sortable && sortable)
                            returnList.Add(fields[i].Name);
                        else if (fields[i].Filterable && filterable)
                            returnList.Add(fields[i].Name);
                        else if (fields[i].Facetable && facetable)
                            returnList.Add(fields[i].Name);
                    return returnList.ToArray();
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(MakeLogPrefix(userId, dataSetName) + "IndxCloudInternalAPI.GetFields exception" + ex.ToString());
                throw;
            }
        }

        /// <summary>
        /// Returns status of the system see the model
        /// class for details.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        internal SystemStatus GetState(string dataSetName, string userId)
        {
            try
            {
                var engine = FindInstance(dataSetName, userId, true);
                if (engine == null)
                    return null;
                return engine.Status;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(MakeLogPrefix(userId, dataSetName) + "IndxCloudInternalAPI.GetState exception" + ex.ToString());
                throw;
            }
        }

        internal void Load(string dataSetName, string userId, Stream jsonData, ProcessMonitor pm)
        {
            var instance = FindInstance(dataSetName, userId, true);
            instance.Load(jsonData, pm);
        }
        internal bool LoadFromDatabase(string dataSetName, string userId, ProcessMonitor monitor)
        {
            var instance = FindInstance(dataSetName, userId, true);
            if (instance == null)
            {
                monitor.Succeeded = false;
                monitor.MarkFinished();
                return false;
            }
            else
            {
                if (instance.DocumentFields == null)
                {
                    if (instance.LoadDocumentFieldsFromDb())
                        return false;
                }

                instance.LoadFromDatabaseSync(monitor);
                return true;
            }
        }
        internal async Task<(bool success, string errorMessage)> LoadJsonStreamAsync(string dataSetName, string userId, Stream jsonData)
        {
            var instance = FindInstance(dataSetName, userId, true);
            if (instance == null)
                return (false, $"{nameof(LoadJsonStreamAsync)} SearchEngine not found");
            var pm = new ProcessMonitor();
            await instance.LoadAsync(jsonData, pm);
            return (pm.Succeeded, pm.ErrorMessage);
        }
        /// <summary>
        /// Performs a search. See the model class for details.
        /// Make sure to check for search readiness after a call
        /// to DoIndexAsync.
        /// </summary>
        /// <param name="cloudQuery"></param>
        /// <param name="dataSetName"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        internal Result Search(Indx.CloudApi.CloudQuery cloudQuery, string dataSetName, string userId)
        {
            try
            {
                var engine = FindInstance(dataSetName, userId, true);
                if (engine == null)
                    return Result.MakeEmptyResult();
                Query query = FromCloudQuery2Query(cloudQuery, engine);
                return engine.Search(query);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(MakeLogPrefix(userId, dataSetName) + "IndxCloudInternalAPI.Search exception" + ex.ToString());
                throw;
            }
        }
        #endregion Internal Methods

        #region Private Fields
        private readonly object _dictionaryLock = new();
        private readonly Dictionary<string, SearchEngineInstance> _instances = [];
        private readonly ILogger<IndxCloudInternalApi> _logger;
        #endregion Private Fields

        #region Private Constructors
        private IndxCloudInternalApi(string searchDbConnectionString)
        {
            _logger = Indx.Utilities.ILoggerFactory.Create<IndxCloudInternalApi>(logFileName);
            SearchDbConnectionString = searchDbConnectionString;
        }
        #endregion Private Constructors

        #region Private Methods
        private void InitializeSystem()
        {
            _logger.Log(LogLevel.Information, $"{nameof(IndxCloudInternalApi)}.{nameof(InitializeSystem)} starting up");
            if (string.IsNullOrEmpty(SearchDbConnectionString))
            {
                _logger.LogError($"{nameof(IndxCloudInternalApi)}.{nameof(InitializeSystem)} SearchDbConnectionString is null or empty");
                throw new InvalidOperationException("SearchDbConnectionString is null or empty");
            }
            // find registered users in the search database
            try
            {
                var sqLiteManager = new SqLiteManager(SearchDbConnectionString);
                if (!sqLiteManager.DatabaseExists())
                {
                    _logger.LogInformation($"{nameof(IndxCloudInternalApi)}.{nameof(InitializeSystem)} no database found at {SearchDbConnectionString}");
                    return;
                }
                ;
                // invariant; database exists
                var users = sqLiteManager.GetUsers();
                foreach (var user in users)
                {
                    var dataSets = sqLiteManager.GetUserDataSets(user);
                    foreach (var dataSet in dataSets)
                    {
                        var instance = FindInstance(dataSet, user, true);
                        if (instance == null)  // fields in dataset not configured properly
                            continue;
                        var monitor = new ProcessMonitor();
                        if (instance.Persistence == null)
                        {
                            _logger.LogWarning($"{nameof(IndxCloudInternalApi)}.{nameof(InitializeSystem)} instance.Persistence is null for user {user} dataset {dataSet}");
                            continue;
                        }
                        if (instance.Persistence.NumberOfJsonRecords() == 0)
                            continue;
                        instance.LoadFromDatabaseSync(monitor);
                        monitor.WaitForCompletion();
                        monitor = new ProcessMonitor();
                        instance.Index(monitor);
                        monitor.WaitForCompletion();

                        ;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{nameof(IndxCloudInternalApi)}.{nameof(InitializeSystem)} {ex.ToString()}");
                throw;
            }

        }
        private static Query FromCloudQuery2Query(CloudQuery cloudQuery, SearchEngine engine)
        {
            Query query = new Query(cloudQuery.Text, cloudQuery.MaxNumberOfRecordsToReturn)
            {
                CoverageSetup = cloudQuery.CoverageSetup,
                LogPrefix = cloudQuery.LogPrefix,
                CoverageDepth = cloudQuery.CoverageDepth,
                RemoveDuplicates = cloudQuery.RemoveDuplicates,  //!!! need to check if it is ok to set this=true (due to arrays)
                EnableBoost = cloudQuery.EnableBoost,
                EnableCoverage = cloudQuery.EnableCoverage,
                EnableFacets = cloudQuery.EnableFacets,
                SortAscending = cloudQuery.SortAscending,
                SortBy = engine.DocumentFields.GetField(cloudQuery.SortBy),
                TimeOutLimitMilliseconds = cloudQuery.TimeOutLimitMilliseconds
            };
            if (cloudQuery.Filter != null)
                query.Filter = engine.GetFilterFromKey(cloudQuery.Filter.HashString);
            if (cloudQuery.Boosts != null)
            {
                Boost[] boosts = new Boost[cloudQuery.Boosts.Length];
                for (int i = 0; i < cloudQuery.Boosts.Length; i++)
                {
                    var f = engine.GetFilterFromKey(cloudQuery.Boosts[i].FilterProxy.HashString);
                    boosts[i] = engine.CreateBoost(f, cloudQuery.Boosts[i].BoostStrength);
                }
                query.Boosts = boosts;
            }
            return query;
        }

        private static string MakeKey(string dataSetName, string userId)
        {
            return userId + dataSetName;
        }

        private static string MakeLogPrefix(string userId, string dataSetName)
        {
            return "User:" + userId + " dataSet:" + dataSetName + " ";
        }

        private SearchEngine FindInstance(string dataSetName, string userId, bool forceCreate)
        {
            SearchEngine? matcher = null;
            bool found = false;
            SearchEngineInstance? instance;
            string key = MakeKey(dataSetName, userId);
            lock (_dictionaryLock)
            {
                found = _instances.TryGetValue(key, out instance);
                if (!found && forceCreate)
                {
                    var persistence = new Persistence(SearchDbConnectionString, dataSetName, userId);
                    var configuration = persistence.ReadDataSetConfiguration();
                    if (configuration == null)
                        return null;
                    matcher = new SearchEngine(MakeLogPrefix(userId, dataSetName), Indx.Utilities.ILoggerFactory.GetFactory(logFileName),
                       (int)configuration, "")
                    {
                        Persistence = persistence
                    };
                    SearchEngineInstance instances = new() { theInstance = matcher };
                    _instances.Add(key, instances);
                    return matcher;
                }
            }
            if (found && instance != null && instance.theInstance != null)
            {
                matcher = instance.theInstance;
                SearchEngineInstance instances = new() { theInstance = matcher };
                return matcher;
            }
            return matcher;
        }
        #endregion Private Methods

        #region Private Classes
        private sealed class SearchEngineInstance
        {
            #region Internal Fields
            // to provide one dBOperation at a time
            internal readonly object DbLock = new();

            internal SearchEngine? theInstance;
            #endregion Internal Fields
        }
        #endregion Private Classes
    }
}