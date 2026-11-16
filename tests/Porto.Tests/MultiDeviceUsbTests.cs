using Porto.Usb;

internal static class MultiDeviceUsbTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("multidevice discovery includes Apple Android and other portable devices",()=>
        {
            var result=DeviceDiscovery.Read(()=>new(0,[new("usb#vid_05ac#1","iPhone","Apple"),
                new("usb#vid_04e8#2","Telefono Android","Samsung"),new("camera#3","Fotocamera","Vendor")]));
            Fixtures.Check(result.Devices.Count==3&&result.WindowsDeviceCount==3);
            Fixtures.Check(result.Devices.Select(d=>d.Id).Distinct().Count()==3);
        }));
        tests.Add(("multidevice discovery preserves nameless devices with visible metadata warning",()=>
        {
            var result=DeviceDiscovery.Read(()=>new(0,[new("portable#1","","",true)]));
            Fixtures.Check(result.Devices.Count==1&&!string.IsNullOrWhiteSpace(result.Devices[0].Name));
            Fixtures.Check(result.MetadataFailures==1&&result.Detail.Contains("incompleti",StringComparison.Ordinal));
        }));
        tests.Add(("multidevice discovery retries truncated batches without selecting another phone",()=>
        {
            int calls=0;var result=DeviceDiscovery.Read(()=>++calls==1?new(1,[new("old","Old","Apple")]):
                new(0,[new("android","Phone","Other"),new("iphone","Other phone","Apple")]));
            Fixtures.Check(calls==2&&result.Devices.Count==2&&result.Devices.All(d=>d.Id!="old"));
        }));
        tests.Add(("multidevice discovery rejects duplicate and malformed device IDs",()=>
        {
            foreach(string id in new[]{""," ","bad\0id"})Fixtures.Throws<IOException>(()=>DeviceDiscovery.Read(()=>new(0,[new(id,"Phone","Vendor")])));
            Fixtures.Throws<IOException>(()=>DeviceDiscovery.Read(()=>new(0,[new("same","One","Vendor"),new("same","Two","Vendor")])));
        }));
    }
}
