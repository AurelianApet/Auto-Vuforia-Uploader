using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;

namespace VuforiaUpload
{
    class Program
    {
        private const string SITE_DOMAIN = "developer.vuforia.com";
        private const string LOGINID = "issam2100@yahoo.com";
        private const string LOGINPWD = "NewApp0921";


        static int Main(string[] args)
        {
            if(args == null || args.Length < 3)
            {
                OutputUsage();
                return 0x80;
            }
            string strDBName = args[0];
            string strCtrlType = args[1];
            string strTargetName = args[2];

            int iTargetWidth = 0;
            string strImgFile = "";

            if (strCtrlType == "add")
            {
                if (args.Length < 5)
                {
                    OutputUsage();
                    return 0x80;
                }
                iTargetWidth = int.Parse(args[3]);
                strImgFile = args[4];
            }                    

            int result = ProcessUpload(strDBName, strCtrlType, strTargetName, iTargetWidth, strImgFile);

            Console.WriteLine("Press enter key to continue...");
            Console.Read();
            return result;
        }

        static void OutputUsage()
        {
            Console.WriteLine("usage:");
            Console.WriteLine("VuforiaUpload {Cloud Database Name} {add | remove} {Target Name} [Add Target Width] [Add Target Image File Path]");
            Console.WriteLine("Press enter key to continue...");
            Console.Read();
        }

        static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        static int ProcessUpload(string strDBName, string strCtrlType, string strTargetName, int iTargetWidth, string strImgFile)
        {
            int star = 0;
            try
            {
                HttpWebRequest webRequest = null;
                ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidateServerCertificate);
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11| SecurityProtocolType.Tls12| SecurityProtocolType.Tls;

                CookieContainer cookies = new CookieContainer();
                
                ToggleAllowUnsafeHeaderParsing(true); //Added to allow unsafe header parsing. MK 
                
                /////////////////////////////////////////////////////////////Login to site//////////////////////////////////////////////////////////////
                Log("Checking user account available....");                                
                webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/auth/login");
                SetWebRequestProperty(webRequest);
                
                string strParam = "{\"user\":{ \"email\":\"" + LOGINID + "\",\"password\":\"" + LOGINPWD + "\"}}";
                webRequest.CookieContainer = cookies;
                webRequest.Method = "POST";
                webRequest.ContentLength = strParam.Length;
                webRequest.ContentType = "application/json;charset=UTF-8";
                byte[] arrBytes = Encoding.UTF8.GetBytes(strParam);
                webRequest.GetRequestStream().Write(arrBytes, 0, arrBytes.Length);

                HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    Log("Fail in checking user account available! Code=" + webResponse.StatusCode.ToString());
                    return 0x80;
                }

                StreamReader sReader = new StreamReader(webResponse.GetResponseStream());
                string strResponse = sReader.ReadToEnd();
                Log("Check the user account available! Response = " + strResponse);
                ///////////////////////////////////////////////////////////////////////////////////

                Log("Fetching userID....");
                
                webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/vuforiaUtil/getLoggedInUser");
                SetWebRequestProperty(webRequest); 
                webRequest.CookieContainer = cookies;
                webRequest.Method = "GET";
                webRequest.ContentType = "Accept: application/json, text/plain, */*";
                webRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36";
                webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    Log("Fail in fetching uid! Code=" + webResponse.StatusCode.ToString());
                    return 0x80;
                }

                sReader = new StreamReader(webResponse.GetResponseStream());
                strResponse = sReader.ReadToEnd();

                JsonTextParser jsonParser = new JsonTextParser();
                JsonObjectCollection rootObj = (JsonObjectCollection)jsonParser.Parse(strResponse);
                string uid = rootObj["eguid"].GetValue().ToString();
                Log("uid = " + uid);
                Log("Fetching database....");

                webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/project/databases");
                SetWebRequestProperty(webRequest);
                strParam = "{\"account_id\":" + uid + ",\"count\":25,\"page\":1,\"search_key\":0,\"search_value\":\"\",\"sort_order\":\"ASC\",\"sorting\":0,\"starting_index\":0" + "}";
                webRequest.CookieContainer = cookies;
                webRequest.Method = "POST";
                webRequest.ContentLength = strParam.Length;
                webRequest.ContentType = "application/json;charset=UTF-8";
                arrBytes = Encoding.UTF8.GetBytes(strParam);
                webRequest.GetRequestStream().Write(arrBytes, 0, arrBytes.Length);

                webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    Log("Fail in fetching database! Code=" + webResponse.StatusCode.ToString());
                    return 0x80;
                }

                sReader = new StreamReader(webResponse.GetResponseStream());
                strResponse = sReader.ReadToEnd();

                rootObj = (JsonObjectCollection)jsonParser.Parse(strResponse);
                JsonArrayCollection arrObj = (JsonArrayCollection)rootObj["list"];
                Log("Success in parsing database list! database count = " + arrObj.Count);
                Dictionary<string, string> dicDB = new Dictionary<string, string>();
                for (int i = 0; i < arrObj.Count; i++)
                {
                    JsonObjectCollection obj = (JsonObjectCollection)arrObj[i];
                    dicDB.Add(obj["project_name"].GetValue().ToString(), obj["project_id"].GetValue().ToString());
                }
                Log("Finished to parse database list! Parsing count = " + dicDB.Count);
                ////////////////////////////////////////////////////////////////////////

                if (!dicDB.Keys.Contains(strDBName))
                {
                    Log("Requested database can`t be found in database list. Requested database name = " + strDBName);
                    return 0x80;
                }
              
                string strProjectID = dicDB[strDBName];
                ///////////////////////////////////////////////////////////////////////////////////////
                Log("Getting target list...");
                
                webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/project/userDeviceTargetDisplayListing");
                SetWebRequestProperty(webRequest);

                strParam = "{\"dataToBeShownForUser\":\"" + uid + "\",\"sEcho\":1,\"iColumns\":6,\"sColumns\":\"\",\"iDisplayStart\":0,\"iDisplayLength\":500,\"amDataProp\":[0,1,2,3,4,5],\"sSearch\":\"\",\"bRegex\":false,\"asSearch\":[\"\",\"\",\"\",\"\",\"\",\"\"],\"abRegex\":[false,false,false,false,false,false],\"abSearchable\":[true,true,true,true,true,true],\"aiSortCol\":[5],\"asSortDir\":[\"desc\"],\"iSortingCols\":1,\"abSortable\":[false,false,false,false,false,false],\"synch\":false,\"projectId\":\"" + strProjectID + "\",\"projectIds\":[1,2,3],\"isLegacyProject\":\"true\",\"dbListingType\":\"CLOUD_LEGACY\"}";
                webRequest.CookieContainer = cookies;
                webRequest.Method = "POST";
                webRequest.ContentLength = strParam.Length;
                webRequest.ContentType = "application/json;";
                arrBytes = Encoding.UTF8.GetBytes(strParam);
                webRequest.GetRequestStream().Write(arrBytes, 0, arrBytes.Length);

                webResponse = (HttpWebResponse)webRequest.GetResponse();
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    Log("Fail in checking target list! Code=" + webResponse.StatusCode.ToString());
                    return 0x80;
                }

                sReader = new StreamReader(webResponse.GetResponseStream());
                strResponse = sReader.ReadToEnd();
                Log("Success in getting target list! Parsing list...");

                Dictionary<string, Target> dicTarget = new Dictionary<string, Target>();
                                
                rootObj = (JsonObjectCollection)jsonParser.Parse(strResponse);
                arrObj = (JsonArrayCollection)rootObj["aaData"];
                for (int i = 0; i < arrObj.Count; i++)
                {
                    JsonObjectCollection obj = (JsonObjectCollection)arrObj[i];
                    dicTarget.Add(obj["target_name"].GetValue().ToString(), new Target(
                        obj["target_id"].GetValue().ToString(),
                        obj["target_name"].GetValue().ToString(),
                        //int.Parse(obj["recos"].GetValue().ToString()),
                        int.Parse(obj["augmentable_rating"].GetValue().ToString()),
                        obj["status"].GetValue().ToString()
                        ));
                }

                Log("Finish to parse target list! Parseing count = " + dicTarget.Count);
                //////////////////////////////////////////////////////////////////////////////////////

                strTargetName = strTargetName.Trim();
                if (strCtrlType == "add")
                {
                    Log("start to add target...");
                    Log("checking target information...");
                    string strRegEx = "^[a-zA-Z0-9_]{1,64}$";
                    if (!Regex.Match(strTargetName, strRegEx).Success)
                    {
                        Log("target name error! 1 ~ 64 letters. Target name: " + strTargetName);
                        return 0x80;
                    }

                    if (iTargetWidth < 1)
                    {
                        Log("Target width error! Must be larger than 0.");
                        return 0x80;
                    }

                    if (!File.Exists(strImgFile))
                    {
                        Log("There is no target image file.");
                        return 0x80;
                    }
                    FileInfo imgFileInfo = new FileInfo(strImgFile);
                    string[] arrValidImgExt = {".jpg", ".png", ".jpeg", ".JPG", ".PNG", ".JPEG"};
                    if (!arrValidImgExt.Contains(imgFileInfo.Extension))
                    {
                        Log("Target image must be JPG/PNG file. file: " + strImgFile);
                        return 0x80;
                    }

                    if (dicTarget.Keys.Contains(strTargetName))
                    {
                        Log("The same name target already exists. Target name = " + strTargetName);
                        return 0x81;
                    }

                    Log("Finish to check target information! Prepare uploading...");

                    string boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");
                    
                    webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/singleCloudTarget/createCloudTarget");

                    SetWebRequestProperty(webRequest);

                    webRequest.CookieContainer = cookies;
                    webRequest.Method = "POST";
                    webRequest.ContentType = "multipart/form-data; boundary=" + boundary;
                    webRequest.KeepAlive = true;

                    MemoryStream postDataStream = new MemoryStream();
                    byte[] buffer = null;
                    Encoding enc = Encoding.UTF8;

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "width", iTargetWidth));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"",
                        "fileData[1]"));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"",
                        "showAdminBreadCrumb"));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "dataRequestedForUserId", uid));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "dataRequestedForUsername", "XiaoWang"));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "TARGET_TYPE", "singleDevice"));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "PROJECT_ID", strProjectID));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "projectName", strDBName));
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}",
                        "targetName", strTargetName));
                    
                    postDataStream.Write(buffer, 0, buffer.Length);

                    buffer = enc.GetBytes("\r\n--" + boundary + "\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);
                    buffer = enc.GetBytes(string.Format("Content-Disposition: form-data;"
                        + "name=\"{0}\";"
                        + "filename=\"{1}\""
                        + "\r\nContent-Type: image/jpeg\r\n\r\n",
                        "fileData[0]",
                        Path.GetFileName(strImgFile)));

                    postDataStream.Write(buffer, 0, buffer.Length);
                    

                    FileStream fileStream = new FileStream(strImgFile, FileMode.Open, FileAccess.Read);
                    buffer = new byte[1024];
                    int bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        postDataStream.Write(buffer, 0, bytesRead);
                    }
                    fileStream.Close();

                    buffer = enc.GetBytes("\r\n--" + boundary + "--\r\n");
                    postDataStream.Write(buffer, 0, buffer.Length);

                    webRequest.ContentLength = postDataStream.Length;

                    postDataStream.Position = 0;
                    byte[] tempBuffer = new byte[postDataStream.Length];
                    postDataStream.Read(tempBuffer, 0, tempBuffer.Length);
                    postDataStream.Close();
                    
                    Stream reqStream = webRequest.GetRequestStream();
                    reqStream.Write(tempBuffer, 0, tempBuffer.Length);
                    reqStream.Close();

                    webResponse = (HttpWebResponse)webRequest.GetResponse();
                    if (webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Log("Fail to upload target! Code=" + webResponse.StatusCode.ToString());
                        return 0x80;
                    }

                    sReader = new StreamReader(webResponse.GetResponseStream());
                    strResponse = sReader.ReadToEnd();
                    if (strResponse == "success")
                    {
                        Log("Success in uploading target!");
                    }
                    else
                    {
                        Log("Fail in uploading target! Server response = " + strResponse);
                    }
                }
                else if (strCtrlType == "remove")
                {
                    Log("Start to delete target...");
                    Log("Checking the target information...");
                    if (!dicTarget.Keys.Contains(strTargetName))
                    {
                        Log("Target to delete is not registered. Target name = " + strTargetName);
                        return 0x80;
                    }

                    Target delTarget = dicTarget[strTargetName];
                    if (delTarget.Status == Target.STATUS_PENDING)
                    {
                        Log("Target to delete can`t be deleted because it`s pending.");
                        return 0x80;
                    }

                    Log("Finish to check target inforamtion! Requesting to delete...");

                    webRequest = (HttpWebRequest)WebRequest.Create("https://" + SITE_DOMAIN + "/targetmanager/project/deleteDatabaseTargets");
                    SetWebRequestProperty(webRequest);
                    strParam = "device_target_listing=device_target_listing&" +
                        "project_id_device="+ strProjectID+ "&" +
                        "typeDevice=CLOUD&" +
                        "projectNameDevice="+strDBName+ "&"+
                        "project_status=ACTIVE&" +
                        "TARGET_IDS="+delTarget.ID+"%3A%3A&" +
                        "isSelectedAll=false";
                    webRequest.CookieContainer = cookies;
                    webRequest.Method = "POST";
                    webRequest.ContentLength = strParam.Length;
                    webRequest.ContentType = "application/x-www-form-urlencoded;charset=UTF-8"; //MKZ 22-03-2019 chnaged the content type
                    arrBytes = Encoding.UTF8.GetBytes(strParam);
                    Stream reqStream = webRequest.GetRequestStream();
                    reqStream.Write(arrBytes, 0, arrBytes.Length);
                    reqStream.Close();

                    webResponse = (HttpWebResponse)webRequest.GetResponse();
                    if (webResponse.StatusCode != HttpStatusCode.OK)
                    {
                        Log("Fail to delete target! Code=" + webResponse.StatusCode.ToString());
                        return 0x80;
                    }

                    sReader = new StreamReader(webResponse.GetResponseStream());
                    strResponse = sReader.ReadToEnd();
                    Log("Finish to delete target!");
                }
                else
                {
                    Log("Requested type is not correct. Support:add / remove, requested type = " + strCtrlType);
                    return 0x80;
                }
            }
            catch (Exception ex)
            {
                Log("An error occurs while uploading.", ex);
                return 0x80;
            }

            return star;
        }

        static void SetWebRequestProperty(HttpWebRequest webReq)
        {
            webReq.Credentials = CredentialCache.DefaultCredentials;
            webReq.UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; Trident/6.0)";
            webReq.KeepAlive = true;
        }
                
        static void Log(string strMsg)
        {
            Log(strMsg, null);
        }
        static void Log(string strMsg, Exception ex)
        {
            if (ex != null)
                strMsg += " ex:" + ex.Message;

            Console.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss} ===> {1}", DateTime.Now, strMsg));
        }


        //Added MKZ 03-21-2019 for unsafe headers and CR LF problem
        public static bool ToggleAllowUnsafeHeaderParsing(bool enable)
        {
            //Get the assembly that contains the internal class
            Assembly assembly = Assembly.GetAssembly(typeof(SettingsSection));
            if (assembly != null)
            {
                //Use the assembly in order to get the internal type for the internal class
                Type settingsSectionType = assembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (settingsSectionType != null)
                {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created already invoking the property will create it for us.
                    object anInstance = settingsSectionType.InvokeMember("Section",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null)
                    {
                        //Locate the private bool field that tells the framework if unsafe header parsing is allowed
                        FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null)
                        {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, enable);
                            return true;
                        }

                    }
                }
            }
            return false;
        }
    }
}
