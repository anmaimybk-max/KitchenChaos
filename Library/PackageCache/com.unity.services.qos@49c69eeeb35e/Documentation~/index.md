# QoS SDK

The QoS SDK provides connectivity Quality of Service (QoS) measurements from Unity clients to supported Unity
services running in multiple geographic regions.

Using this SDK in conjunction with [Multiplay hosting](https://docs.unity.com/multiplay/shared/welcome-to-multiplay.html) and [Matchmaker](https://docs.unity.com/multiplay/shared/matchmaker-overview.html) can enable matchmaking that considers clients' latency
measurements in match groupings.

## Supported versions

The QoS SDK supports Unity 6000.0 and newer. Invoking the measurement APIs in unsupported Unity versions
will throw an exception.

## Supported platforms

QoS measurements use UDP on every platform except WebGL. On WebGL, the SDK measures over WebTransport instead, so the
player must run in a browser that supports WebTransport.

A server that cannot be measured, for example from a browser without WebTransport support, reports an invalid
measurement of `int.MaxValue` latency and 100% packet loss. `GetSortedRelayQosResultsAsync` and
`GetSortedMultiplayQosResultsAsync` drop those results. `GetSortedQosResultsAsync` and `GetQosResultsAsync` return
them, so check the latency and packet loss before treating a result as a usable region.

## Using the QoS SDK

### Import packages

```c#
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Qos;
```

### Player authentication

You must authenticate all players. The simplest way to authenticate players is through
[anonymous authentication](https://docs.unity.com/authentication/UsingAnonSignIn.html) using the
[Authentication SDK](https://docs.unity.com/authentication/IntroUnityAuthentication.html). However, other methods are also supported.

The following code sample demonstrates how to authenticate a player using anonymous authentication.

```c#
try
{
    await UnityServices.InitializeAsync();
    await AuthenticationService.Instance.SignInAnonymouslyAsync();
    var playerID = AuthenticationService.PlayerId;
}
catch (Exception e)
{
    Debug.Log(e);
}
```

### Run QoS measurements

You can obtain a list of QoS results sorted by latency and packet loss through a single call to the QoS SDK. Specify the Unity
service to get a list of QoS measurements for all regions available for that service.

The following code sample demonstrates how to use the `GetSortedQosResultsAsync` method.

```c#
try
{
    var serviceName = "multiplay";
    var qosResults = await QosService.Instance.GetSortedQosResultsAsync(serviceName, null);
}
catch (Exception e)
{
    Debug.Log(e);
}
```

If you need a more refined list of regions, obtain that list of regions before passing it to the QoS SDK. The SDK only returns
QoS measurements for the specified regions in such a case.

The following code sample demonstrates how to get measurements for a refined set of regions. Obtaining the list of regions is
outside the scope of the QoS SDK.

```c#
try
{
    var regions = new List<string>();
    // Populate `regions` with a subset of multiplay regions (outside of the scope of this SDK)

    var serviceName = "multiplay";
    var qosResults = await QosService.Instance.GetSortedQosResultsAsync(serviceName, regions);
}
catch (Exception e)
{
    Debug.Log(e);
}
```

## SDK Documentation
The SDK is documented using XML comments. These will work with most IDEs to provide IntelliSense and inline documentation.
