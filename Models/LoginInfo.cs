namespace IndxCloudApi.Models
{
    /// <summary>
    /// Class to support JSON serialization of user credentials
    /// </summary>
    public class LoginInfo
    {
        #region Public Properties
        /// self explanatory
        public string UserEmail { get; set; } = "";
        /// self explanatory
        public string UserPassWord { get; set; } = "";
        #endregion Public Properties
    }
}