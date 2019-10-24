using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Google
{
    public partial class GoogleAlertAPI
    {
        private string _auth_id = string.Empty;
        private string _auth_token = string.Empty;
        private readonly Hashtable _alertTable = new Hashtable();
        private readonly HttpClient _client;
        private readonly HttpClientHandler _httpClientHandler;
        private readonly CookieContainer _cookieContainer = new CookieContainer();

        private const string SCRIPT_JSON_TAG = "window.STATE";
        private const string SCRIPT_JSON_TAG_REGEX = SCRIPT_JSON_TAG + "=(.*]]])";

        public GoogleAlertAPI(string SID, string HSID, string SSID)
        {   
            _httpClientHandler = new HttpClientHandler() {
                                        CookieContainer = _cookieContainer,
                                        UseCookies = true,
                                        UseDefaultCredentials = false
                                    };
            _client = new HttpClient(_httpClientHandler);
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml");
            //client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.8");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Charset", "ISO-8859-1");

            _cookieContainer.Add(new Cookie("SID", SID, "/", ".google.com"));
            _cookieContainer.Add(new Cookie("HSID", HSID, "/", ".google.com"));
            _cookieContainer.Add(new Cookie("SSID", SSID, "/", ".google.com"));

            Login();
        }

        private void Login()
        {
            Task<string> alertTask = PostUrlString("https://www.google.com/alerts?hl=en&", null);
            Task.WaitAll(alertTask);
            var body = alertTask.Result;
            List<string> script_list = GetElements(body,"//script");
            foreach (string script in script_list)
            {
                if (script.Contains(SCRIPT_JSON_TAG))
                {
                    string json_str = Regex.Match(script, SCRIPT_JSON_TAG_REGEX).Groups[1].Value;
                    JArray json = JArray.Parse(json_str);
                    if (json[1] == null)
                    {
                        break;
                    }                    
                    JToken alert_item_token = json[1];
                    if (!IsNullOrEmpty(alert_item_token))
                    {
                        foreach (var alert_item in alert_item_token[1])
                        {
                            _alertTable[alert_item[1].ToString()] = alert_item[1][5][0][10].ToString();
                        }
                    }
                    _auth_id = json[2].ToString();
                    _auth_token = json[1][5][0][13].ToString();
                }
            }
            if (string.IsNullOrEmpty(_auth_id) || string.IsNullOrEmpty(_auth_token))
            {
                throw new Exception("Login error.");
            }
        }

        public List<Alert> GetAlerts()
        {
            List<Alert> alert_list = new List<Alert>();
            if (!string.IsNullOrEmpty(_auth_id))
            {
                Task<string> alertTask = PostUrlString("https://www.google.com/alerts?hl=en&", null);
                Task.WaitAll(alertTask);
                string body = alertTask.Result;

                List<string> script_list = GetElements(body, "//script");
                foreach (string script in script_list)
                {
                    if (script.Contains(SCRIPT_JSON_TAG))
                    {
                        string json_str = Regex.Match(script, SCRIPT_JSON_TAG_REGEX).Groups[1].Value;
                        JArray json = JArray.Parse(json_str);
                        JToken alert_item_token = json[1];
                        
                        if (IsNullOrEmpty(alert_item_token) || json[1] == null || json[1][1] == null || json[1][1].Count() <= 0)
                            break;
                        
                        foreach (var alert_item in json[1][1])
                        {
                            Alert alert = new Alert
                            {
                                feedUrl = $"https://www.google.com/alerts/feeds/{alert_item[3]}/{alert_item[2][6][0][11]}",
                                id = alert_item[1].ToString(),
                                query = alert_item[2][3][1].ToString()
                            };

                            Enum.TryParse(alert_item[2][5].ToString(), out alert.howMany);
                            Enum.TryParse(alert_item[2][6][0][4].ToString(), out alert.howOften);
                            alert.language = GetEnumValueFromDescription<Language>(alert_item[2][3][3][1].ToString());
                            alert.region = GetEnumValueFromDescription<Region>(alert_item[2][3][3][2].ToString());
                            
                            if (alert_item[2][4] != null)
                                for (int i = 0; i < alert_item[2][4].Count(); i++)
                                    alert.sources.Add(GetEnumValueFromDescription<Sources>(alert_item[2][4][i].ToString()));

                            if (alert_item[2][6][0][1].ToString() == "1")
                                alert.deliveryTo = DeliveryTo.Email;
                            else
                                alert.deliveryTo = DeliveryTo.Feed;
                            
                            alert_list.Add(alert);
                        }
                        _auth_id = json[3].ToString();
                        break;
                    }
                }
            }
            return alert_list;
        }

        public string CreateAlert(Alert alert)
        {  
            string id = string.Empty;
            if (!string.IsNullOrEmpty(_auth_id) && alert != null)
            {
                List<KeyValuePair<string,string>> postData = new List<KeyValuePair<string,string>>();
                postData.Add(new KeyValuePair<string,string>("params",CreatePostParameter(alert,false)));
                Task<string> createTask = PostUrlString("https://www.google.com/alerts/create?hl=en&", postData);
                Task.WaitAll(createTask);
                string body = createTask.Result;
                JArray json = JArray.Parse(body);
                if (IsNullOrEmpty(json) || json.Count()<5)
                {
                    throw new Exception("Create alert error!");
                }
                else
                {
                    id = json[4][0][1].ToString();
                    _alertTable[id] = json[4][0][3][6][0][11].ToString();
                    if (alert.deliveryTo == DeliveryTo.Feed)
                    {
                        
                        var link_text = json[4][0][2].ToString();
                        Match match = Regex.Match(link_text, @"/alerts/feeds/[\w^/]+/[\w^/]+",RegexOptions.Compiled);
                     
                        if (match.Success)
                            alert.feedUrl = "https://www.google.com" + match.Value;
                    }
                    
                }
            }
            return id;
        }

        public bool DeleteAlert(string alert_id)
        {
            if (!string.IsNullOrEmpty(_auth_id) && !string.IsNullOrEmpty(alert_id))
            {
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("params", "[null,\"" + alert_id + "\"]"));
                Task<string> deleteTask = PostUrlString("https://www.google.com/alerts/delete?hl=en&", postData);
                Task.WaitAll(deleteTask);
                return true;
            }
            return false;
        }

        public string ModifyAlert(Alert alert)
        {
            string id = string.Empty;
            if (!string.IsNullOrEmpty(_auth_id) && (alert != null) && (!string.IsNullOrEmpty(alert.id)))
            {
                List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                postData.Add(new KeyValuePair<string, string>("params", CreatePostParameter(alert, true)));
                Task<string> createTask = PostUrlString("https://www.google.com/alerts/modify?hl=en&", postData);
                Task.WaitAll(createTask);
                string body = createTask.Result;
                JArray json = JArray.Parse(body);
                id = json[4][0][1].ToString();
                _alertTable[id] = json[4][0][3][6][0][11].ToString();
            }
            return id;
        }

        #region Private Methods

        private string CreatePostParameter(Alert alert, bool isEdit, string email = "")
        {   
            string domainExt = "com";
            string feedKey = "1";
            string _email = email;
            HowOften _howOften = alert.howOften;
            if (alert.deliveryTo == DeliveryTo.Feed)
            {
                feedKey = "2";
                _howOften = HowOften.AsItHappens;
                _email = string.Empty;
            }

            if (!string.IsNullOrEmpty(_email))
                domainExt = _email.Substring(_email.LastIndexOf(".") + 1);

            string _sources = string.Empty;
            foreach (var source in alert.sources)
            {
                if (string.IsNullOrEmpty(_sources))
                    _sources += GetDescriptionFromEnumValue(source);
                else
                    _sources += "," + GetDescriptionFromEnumValue(source);
            }
            _sources = "[" + _sources +"]";
            
            if (_sources == "[]")
                _sources = "null";
            
            string howOften = string.Empty;
            switch(_howOften){
                case HowOften.AsItHappens:
                    howOften = "[]," + (int)_howOften;
                    break;
                case HowOften.AtMostOnceADay:
                    howOften = "[null,null,3]," + (int)_howOften;
                    break;
                case HowOften.AtMostOnceAWeek:
                    howOften = "[null,null,3,3]," + (int)_howOften;
                    break;
            }

            string update = string.Empty;
            if (isEdit)
                update = "\"" + alert.id + "\",";
            
            string flagLanguage = "1";
            Language _language = alert.language;
            if (alert.language == Language.AnyLanguage)
            {
                _language = Language.English;
                flagLanguage = "0";
            }

            string flagRegion = "1";
            Region _region = alert.region;
            if (alert.region == Region.AnyRegion)
            {
                _region = Region.UnitedStates;
                flagRegion = "0";
            }

            string query = alert.query.Replace("\"", "\\\"");

            return "[null," + update + "[null,null,null,[null,\"" + query + "\",\"" + domainExt + "\",[null,\"" + GetDescriptionFromEnumValue(_language) + "\",\"" + GetDescriptionFromEnumValue(_region) + "\"],null,null,null," + flagRegion + "," + flagLanguage + "]," + _sources + "," + (int)alert.howMany + ",[[null," + feedKey + ",\"" + _email + "\"," + howOften + ",\"en-US\"," + (isEdit ? "1" : "null") + ",null,null,null,null,\"" + (isEdit ? (string)this._alertTable[alert.id] : "0") + "\",null,null,\"" + this._auth_token + "\"]]]]";
        }

        private async Task<string> PostUrlString(string url, List<KeyValuePair<string, string>> postData)
        {
            if (postData == null)
                postData = new List<KeyValuePair<string, string>>();
            
            HttpContent postContent = new FormUrlEncodedContent(postData);
            if ((postData != null) && (postData.Count == 1) && postData[0].Key == "params")
                url = url + "x=" + this._auth_id;
            
            HttpResponseMessage response = await _client.PostAsync(url, postContent);
            response.EnsureSuccessStatusCode();
            Task<string> body = response.Content.ReadAsStringAsync();
            return await Task.Run(() => body.Result);
        }

        private List<string> GetElements(string body, string xpath)
        {
            List<string> list = new List<string>();
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(body);
            HtmlAgilityPack.HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(xpath);
            
            foreach (var node in nodes)
                list.Add(node.InnerHtml);
            
            return list;
        }

        private static string GetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute == null ? value.ToString() : attribute.Description;
        }

        private static T GetEnumValueFromDescription<T>(string description)
        {
            var type = typeof(T);
            if (!type.IsEnum)
                throw new ArgumentException();
            FieldInfo[] fields = type.GetFields();
            var field = fields
                            .SelectMany(f => f.GetCustomAttributes(
                                typeof(DescriptionAttribute), false), (
                                    f, a) => new { Field = f, Att = a })
                            .Where(a => ((DescriptionAttribute)a.Att)
                                .Description == description).SingleOrDefault();
            return field == null ? default(T) : (T)field.Field.GetRawConstantValue();
        }

        public static bool IsNullOrEmpty(JToken token)
        {
            return (token == null) ||
                   (token.Type == JTokenType.Array && !token.HasValues) ||
                   (token.Type == JTokenType.Object && !token.HasValues) ||
                   (token.Type == JTokenType.String && token.ToString() == String.Empty) ||
                   (token.Type == JTokenType.Null);
        }

        #endregion
    }
}
