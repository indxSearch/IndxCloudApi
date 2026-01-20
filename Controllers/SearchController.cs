using Indx.Api;
using Indx.CloudApi;
using Indx.Core;
using Indx.Storage;
using Indx.Utilities;
using IndxCloudApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace IndxCloudApi.Controllers
{
    /// <summary>
    /// Provides API endpoints for managing, analyzing, indexing, and searching JSON-based datasets. Supports operations
    /// such as dataset creation, deletion, field configuration, data loading, and executing search queries. All
    /// endpoints require authentication and operate on datasets associated with the authenticated user.
    /// </summary>
    /// <remarks>This controller exposes a comprehensive set of endpoints for working with document-oriented
    /// datasets in a search engine context. Operations include analyzing JSON data, configuring
    /// searchable/filterable/sortable fields, creating and combining filters, loading data from various sources, and
    /// performing search queries. Most endpoints require a valid dataset name and user authentication via JWT bearer
    /// tokens. Responses typically indicate success or provide detailed error information for invalid requests, such as
    /// unauthorized access or invalid dataset names. The controller is intended for use in scenarios where users need
    /// to manage and query large collections of JSON documents with flexible field and filter configurations.</remarks>
    [Route("api")]
    [ApiController]
    public class SearchController : Controller
    {
        #region Public Methods
        /// <summary>
        /// As Analyze but handles a stream as input text.
        /// </summary>
        /// <returns></returns>
        [HttpPost("AnalyzeStreamAsync/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public async Task<ActionResult<SystemStatus>> AnalyzeStreamAsync(string dataSetName)
        {
            HttpContext.Request.EnableBuffering();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngineForInit(dataSetName, userId);
            if (matcher == null)
                return BadRequest("AnalyzeStreamAsync non existing dataset name or configuration");
            var state = matcher.Status;
            if (state.InvalidDataSetName)
                return BadRequest("invalid dataSetName");
            var pm = new ProcessMonitor();
            matcher.Init(HttpContext.Request.Body, pm);
            pm.WaitForCompletion();
            if (!pm.Succeeded)
                return BadRequest("Analyze failed, likely invalid json data");
            if (matcher.DocumentFields == null)
                return BadRequest("Analyze failed, DocumentFields==null");
            if (matcher.Persistence == null)
                return BadRequest("Analyze failed, Persistence==null");
            matcher.Persistence.SaveDocumentFields(matcher.DocumentFields.GetSerialized());
            return Ok(state);
        }

        /// <summary>
        /// Analyze the fields of a string containing json. Since json may be invalid, which will cause
        /// a 400 error, it is sent as plain text.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [RequestSizeLimit(2_000_000_000)]
        [HttpPost("AnalyzeString/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<SystemStatus> AnalyzeString(string dataSetName, [FromBody] string jsonData)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (string.IsNullOrEmpty(jsonData))
                return BadRequest("null or empty jsonData argument");
            var state = IndxCloudInternalApi.Manager.GetState(dataSetName, userId);
            if (state == null || state.InvalidDataSetName)
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngineForInit(dataSetName, userId);
            if (matcher == null)
                return BadRequest("AnalyzeString non existing dataset name or configuration");
            var df = DocumentFields.Analyze(jsonData, out string error);
            if (!string.IsNullOrEmpty(error) || df == null)
                return BadRequest(error);
            matcher.SetDocumentFieldsInternal(df);
            if (matcher.Persistence == null)
                return BadRequest("Analyze failed, Persistence==null");
            matcher.Persistence.SaveDocumentFields(df.GetSerialized());
            return state;
        }

        /// <summary>
        /// CreateRangeFilter will create a RangeFilter which may be passed to any search.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="combineFilters"></param>
        /// <returns></returns>
        [HttpPut("CombineFilters/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<FilterProxy> CombineFilters(string dataSetName, [FromBody] CombinedFilterProxy combineFilters)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("CombineFilters, non existing dataset name");
            var fa = matcher.GetFilterFromKey(combineFilters.A.HashString);
            var fb = matcher.GetFilterFromKey(combineFilters.B.HashString);
            matcher.LoadFilters(new Filter[] { fa, fb });
            Filter result;
            if (combineFilters.UseAndOperation)
                result = fa & fb;
            else
                result = fa | fb;
            var filterProxy = new FilterProxy(result.SerializedKey);
            return Ok(filterProxy);
        }

        /// <summary>
        /// CreateBoost will create a Boost setu which may be passed to any search.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="boost"></param>
        /// <returns></returns>
        [HttpPut("CreateBoost/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<BoostProxy> CreateBoost(string dataSetName, [FromBody] BoostProxy boost)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if ((matcher == null))
                return BadRequest("CreateBoost non existing dataset name");
            var filter = matcher.GetFilterFromKey(boost.FilterProxy.HashString);
            if (filter == null)
                return BadRequest("invalid filter arguments");
            var bf = matcher.CreateBoost(filter, boost.BoostStrength);
            return Ok(boost);
        }

        /// <summary>
        /// CreateOrOpen will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// Uses default configuration.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns></returns>
        [HttpPut("CreateOrOpen/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult CreateOrOpen(string dataSetName)
        {
            return CreateOrOpen(dataSetName, 400);
        }

        /// <summary>
        /// CreateOrOpen will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        [HttpPut("CreateOrOpen/{dataSetName}/{configuration}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult CreateOrOpen(string dataSetName, int configuration)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            //    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            //    var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            if (!CoreSearchEngine.ConfigurationExists(configuration))
                return BadRequest("illegal configuration number");
            var persistence = new Persistence(IndxCloudInternalApi.SearchDbConnectionString, dataSetName, userId);
            if (!persistence.DataSetExists())
                persistence.CreateOrOpenDataSet(configuration);
            return Ok();
        }

        /// <summary>
        /// CreateRangeFilter will create a RangeFilter which may be passed to any search.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="rangeFilter"></param>
        /// <returns></returns>
        [HttpPut("CreateRangeFilter/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<FilterProxy> CreateRangeFilter(string dataSetName, [FromBody] RangeFilterProxy rangeFilter)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("CreateRangeFilter non existing dataset name");
            var filter = matcher.CreateRangeFilter(rangeFilter.FieldName, rangeFilter.LowerLimit, rangeFilter.UpperLimit);
            if (filter == null)
                return BadRequest("invalid filter arguments");
            var filterProxy = new FilterProxy(filter.SerializedKey);
            return Ok(filterProxy);
        }

        /// <summary>
        /// CreateValueFilter will create a ValueFilter which may be passed to any search.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="valueFilter"></param>
        /// <returns></returns>
        [HttpPut("CreateValueFilter/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<FilterProxy> CreateValueFilter(string dataSetName, [FromBody] ValueFilterProxy valueFilter)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            //if (!IndxCloudInternalApi.Manager.ReloadAndIndex(dataSetName, userId, false, out SearchEngine matcher))
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("CreateRangeFilter non existing dataset name");
            var filter = matcher.CreateValueFilter(valueFilter.FieldName, valueFilter.Value);
            if (filter == null)
                return BadRequest("invalid filter arguments, filter value must have tostring implementation");
            var filterProxy = new FilterProxy(filter.SerializedKey);
            return Ok(filterProxy);
        }

        /// <summary>
        /// DeleteDataSet, will delete the entire dataSet including all contained Documents.
        /// It will take effect immediately and the data cannot be recovered. In case of
        /// non existing dataset BadRequest will be returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns></returns>
        [HttpDelete("DeleteDataSet/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult DeleteDataSet(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var persistence = new Persistence(IndxCloudInternalApi.SearchDbConnectionString, dataSetName, userId);
            if (!persistence.DataSetExists())
                return BadRequest("Attempt to delete non exixting dataset");

            // Clean up in-memory SearchEngine instance before deleting from DB
            IndxCloudInternalApi.Manager.DisposeDataSetInstance(dataSetName, userId);

            persistence.DeleteDataSet();
            return Ok();
        }

        /// <summary>
        /// GetAllFields will return the fields found during analyze. If the client is
        /// not authenticated it will return null
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetallFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetAllFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, true, false, false, false, false, false);
        }

        /// <summary>
        /// GetFacetableFields will return the array of these field names of. Use SetFacetableFields
        /// to assign this property. If the client is not authenticated null is returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetFacetableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetFacetableFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, false, false, false, false, true, false);
        }

        /// <summary>
        /// GetFilterableFields will return the array of these field names of. Use SetFilterAbleFields
        /// to assign this property. If the client is not autenticated null is returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetFilterableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetFilterableFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, false, false, false, true, false, false);
        }

        /// <summary>
        /// returns the raw json records as string[] for the keys
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        [HttpPost("GetJson/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetJson(string dataSetName, [FromBody] long[] keys)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var engine = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (engine == null)
                return BadRequest("GetJson non existing dataset name");
            if (engine.Status.SystemState == SystemState.Created || engine.Status.SystemState == SystemState.Loading)
                return BadRequest("GetJson invalid status, no data loaded or loading in progress");
            var jsonStrings = new string[keys.Length];
            for (int i = 0; i < jsonStrings.Length; i++)
                jsonStrings[i] = engine.GetJsonDataOfKey(keys[i]);
            return jsonStrings;
        }
        /// <summary>
        /// GetStatus will return the status\us of the dataSetName in the search engine. If the client is
        /// not authenticated it will return null.If the client is asking for a non-existing dataSetName
        /// a SystemState with the field invalidDataSetName= true will be returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetNumberOfJsonRecordsInDb/{dataSetname}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<int> GetNumberOfJsonRecordsInDb(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var persistence = new Persistence(IndxCloudInternalApi.SearchDbConnectionString, dataSetName, userId);
            if (!persistence.DataSetExists())
                return BadRequest("invalid dataSetName");
            return persistence.NumberOfJsonRecords();
        }
        /// <summary>
        /// GetSearchableFields will return the array of these field names of. Use SetIndexAbleField
        /// to assign this property. If the client is not authenticated null is returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetSearchableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetSearchableFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, false, true, false, false, false, false);
        }
        /// <summary>
        /// GetSortableFields will return the array of these field names. Use SetSortableField
        /// to assign this property. If the client is not authenticated null is returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetSortableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetSortableFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, false, false, true, false, false, false);
        }
        /// <summary>
        /// GetStatus will return the status\us of the dataSetName in the search engine. If the client is
        /// not authenticated it will return null.If the client is asking for a non-existing dataSetName
        /// a SystemState with the field invalidDataSetName= true will be returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetStatus/{dataSetname}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<SystemStatus> GetStatus(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var status = IndxCloudInternalApi.Manager.GetState(dataSetName, userId);
            if (status == null)
                return BadRequest("GetStatus failed, status==null");
            return status;
        }

        /// <summary>
        /// GetWordIndexingFields will return the array of these field names. Use SetWordIndexingFields
        /// to assign this property. If the client is not authenticated null is returned.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>SearchState</returns>
        [HttpGet("GetWordIndexingFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetWordIndexingFields(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            return IndxCloudInternalApi.Manager.GetFields(dataSetName, userId, false, false, false, false, false, true);
        }

        /// <summary>
        /// Return the datasets created by current user if any. If no datasets are
        /// found an empty array will be returned.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetUserDatasets")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<string[]> GetUserDataSets()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var persistence = new Persistence(IndxCloudInternalApi.SearchDbConnectionString, "dummy", userId);
            return persistence.GetUserDataSets(userId);
        }

        /// <summary>
        /// IndexDataSet will start indexing of the Loaded documents.
        /// Indexing will run asynchronously and the progress may be monitored by GetStatus.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("IndexDataSet/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<SystemStatus> IndexDataSet(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return BadRequest("unauthorized");
            if (!IndxCloudInternalApi.Manager.DoIndex(dataSetName, userId))
            {
                var status1 = IndxCloudInternalApi.Manager.GetState(dataSetName, userId);
                if (status1 == null)
                    return BadRequest("IndexDataSet failed, DoIndex returned false");
                else
                {
                    if (status1.SystemState == SystemState.Created)
                    {
                        status1.ErrorMessage = "IndexDataSet failed, due to invalidstate, check Load operation completion status";
                        return StatusCode(StatusCodes.Status409Conflict, status1);
                    }
                }
            }

            var status = IndxCloudInternalApi.Manager.GetState(dataSetName, userId);
            if (status == null)
                return BadRequest("IndexDataSet failed, status==null");
            return status;
        }

        /// <summary>
        /// Loads the jsonData into search engine from the database
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>Unauthorized (401), BadRequest(400) or Ok(200)</returns>
        [HttpGet("LoadFromDatabase/{dataSetName}")]
        [EnableCors("AllowAllHeaders")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult> LoadFromDatabaseAsync(string dataSetName)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var pm = new ProcessMonitor();
            var success = IndxCloudInternalApi.Manager.LoadFromDatabase(dataSetName, userId, pm);
            if (!success)
                return BadRequest("LoadFromDatabaseAsync failed, success==null");
            await pm.WaitForCompletionAsync();
            if (!pm.Succeeded)
                return BadRequest(pm.ErrorMessage);
            return Ok();
        }

        /// <summary>
        /// Loads the jsonData into search engine as a stream
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <returns>Unauthorized (401), BadRequest(400) or Ok(200)</returns>
        [HttpPut("LoadStream/{dataSetName}")]
        [EnableCors("AllowAllHeaders")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult> LoadStreamAsync(string dataSetName)
        {
            HttpContext.Request.EnableBuffering();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (HttpContext.Request.ContentLength == null || HttpContext.Request.ContentLength == 0)
                return BadRequest("Empty request body, stream missing");
            var bodyStream = HttpContext.Request.Body;
            bodyStream.Position = 0; // Ensure it's at the beginning
            var pm = new ProcessMonitor();
            if (IndxCloudInternalApi.Manager.Load(dataSetName, userId, bodyStream, pm))
                pm.WaitForCompletion();
            else
                return BadRequest("LoadStreamAsync failed, Load returned false");
            if (!pm.Succeeded)
                return StatusCode(StatusCodes.Status422UnprocessableEntity, pm.ErrorMessage);

            return Ok();
        }

        /// <summary>
        /// Loads the jsonData into search engine as a string
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="jsonData"></param>
        /// <returns>Unauthorized (401), BadRequest(400) or Ok(200)</returns>
        [RequestSizeLimit(2_000_000_000)]
        [HttpPut("LoadString/{dataSetName}")]
        [EnableCors("AllowAllHeaders")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult LoadString(string dataSetName, [FromBody] string jsonData)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(jsonData);
                writer.Flush();
            }
            memoryStream.Position = 0;
            var pm = new ProcessMonitor();
            if (IndxCloudInternalApi.Manager.Load(dataSetName, userId, memoryStream, pm))
                pm.WaitForCompletion();
            else
                return BadRequest("LoadString failed, Load returned false");
            if (!pm.Succeeded)
                return BadRequest(pm.ErrorMessage);
            return Ok();
        }

        /// <summary>
        /// Search will validate the search query and return the search result. If no match is found
        /// the method will return an empty document array. The method will return null if query is invalid.
        /// Max length of search result is truncated to Query.MaxLengthOfSearchResult (pt = 1000).
        /// If the dataSetName is non existing this will be reflected in the field InvalidDataSetName. If the status field
        /// is set invalid it means that the system is still loading or indexing data. Use GetState to monitor
        /// the progress of indexing. An indexing status may also result by a power cycle or restart of the server.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="query"></param>
        /// <returns>SearchResult</returns>
        [HttpPost("Search/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public ActionResult<Indx.Api.Result> Search(string dataSetName, [FromBody] CloudQuery query)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            Indx.Api.Result res = IndxCloudInternalApi.Manager.Search(query, dataSetName, userId);
            return res;
        }

        /// <summary>
        /// SetIndexAbleFields will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        [HttpPut("SetFacetableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult SetFacetableFields(string dataSetName, [FromBody] string[] fields)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("non existing dataSetName");
            var df = matcher.DocumentFields;
            if (df == null)
                return BadRequest("SearchController.SetFacetableFields invalid status");
            foreach (var item in fields)
            {
                var f = df.GetField(item);
                if (f == null)
                    return BadRequest("SearchController.SetFacetableFields non existing fieldname");
                f.Facetable = true;
            }
            return Ok();
        }

        /// <summary>
        /// SetIndexAbleFields will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        [HttpPut("SetFilterableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult SetFilterableFields(string dataSetName, [FromBody] string[] fields)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("non existing dataSetName");
            var df = matcher.DocumentFields;
            if (df == null)
                return BadRequest("SearchController.SetFilterableFields invalid status");
            foreach (var item in fields)
            {
                var f = df.GetField(item);
                if (f == null)
                    return BadRequest("SearchController.SetFilterableFields non existing fieldname");
                f.Filterable = true;
            }
            return Ok();
        }

        /// <summary>
        /// SetIndexAbleFields will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        [HttpPut("SetSearchableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult SetSearchableFields(string dataSetName, [FromBody] (string Name, int Weight)[] fields)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            //IndxCloudInternalApi.Manager.ReloadAndIndex(dataSetName, userId, false, out SearchEngine matcher);
            if (matcher == null)
                return BadRequest("non existing dataSetName");
            var df = matcher.DocumentFields;
            if (df == null)
                return BadRequest("SearchController.SetSearchableFields invalid status");
            foreach (var item in fields)
            {
                var f = df.GetField(item.Name);
                if (f == null)
                    return BadRequest("SearchController.SetSearchableFields non existing fieldname");
                f.Searchable = true;
                f.Weight = (Weight)item.Weight;
            }
            return Ok();
        }

        /// <summary>
        /// SetSortableFields will create a data set where you can insert, save, index and search for DocumentJson.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        [HttpPut("SetSortableFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult SetSortableFields(string dataSetName, [FromBody] string[] fields)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("non existing dataSetName");
            var df = matcher.DocumentFields;
            if (df == null)
                return BadRequest("SearchController.SetSortableFields invalid status");
            foreach (var item in fields)
            {
                var f = df.GetField(item);
                if (f == null)
                    return BadRequest("SearchController.SetSortableFields non existing fieldname");
                f.Sortable = true;
            }
            return Ok();
        }
        /// <summary>
        /// SetWordIndexingFields will set the WordIndexing properties of the fields to true.
        /// Every endpoint of this API will refer to one or more datasets.
        /// </summary>
        /// <param name="dataSetName"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        [HttpPut("SetWordIndexingFields/{dataSetName}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [EnableCors("AllowAllHeaders")]
        public IActionResult SetWordIndexingFields(string dataSetName, [FromBody] string[] fields)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            if (!FileNameValidity.IsValid(dataSetName))
                return BadRequest("invalid dataSetName");
            var matcher = IndxCloudInternalApi.Manager.FindSearchEngine(dataSetName, userId);
            if (matcher == null)
                return BadRequest("non existing dataSetName");
            var df = matcher.DocumentFields;
            if (df == null)
                return BadRequest("SearchController.SetWordIndexingFields invalid status");
            foreach (var item in fields)
            {
                var f = df.GetField(item);
                if (f == null)
                    return BadRequest("SearchController.SetWordIndexingFields non existing fieldname");
                f.WordIndexing = true;
            }
            return Ok();
        }
    }
    #endregion Public Methods
}