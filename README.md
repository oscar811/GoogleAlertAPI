# GoogleAlertAPI
### Update
Since the password authentification no longer exist, we should use Cookies identifiers.


How to get Cookie IDs:
1. Open Chrome Dev Tools (F12)
2. Open "Network" tab
3. Select Checkbox "Preserve log"
4. Open Google SignIn page https://accounts.google.com/ServiceLogin 
5. Enter email, press next
6. Press "Clear" in "Network" tab
7. Enter password and press "Next"
8. In "Network" tab find a row with "challenge?hl=" and select it
9. Open "Cookies" tab and copy SID, SSID, HSID values

### Usage
Create Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
Google.GoogleAlertAPI.Alert alert = new Google.GoogleAlertAPI.Alert();
alert.query = "GoogleAlertAPI";
alert.region = Google.GoogleAlertAPI.Region.Taiwan;
alert.language = Google.GoogleAlertAPI.Language.ChineseTraditional;
alert.deliveryTo = Google.GoogleAlertAPI.DeliveryTo.Feed;
alert.howMany = Google.GoogleAlertAPI.HowMany.AllResults;
alert.sources.Add(Google.GoogleAlertAPI.Sources.Blogs);
alert.sources.Add(Google.GoogleAlertAPI.Sources.Web);
ga.createAlert(alert);
```

List all Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
List<Google.GoogleAlertAPI.Alert> list = ga.getAlerts();
foreach(var alert in list){
  // alert object
}
```

Delete Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
ga.deleteAlert(alert.id);
```

Modify Alert
```csharp
Google.GoogleAlertAPI ga = new Google.GoogleAlertAPI("xxx@gmail.com","XXXXX");
ga.modifyAlert(alert);
```


