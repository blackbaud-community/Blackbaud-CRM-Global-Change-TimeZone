Blackbaud CRM Global Change Example: TimeZone
====================================
A global change allows you to make changes to specific records within your database via criteria that you specify.

This example will teach you the basics of building a CLR-based global change using a Blackbaud Infinity SDK Global Change Spec (CLR) catalog template. The template creates a spec for a CLR-implemented global change operation/definition.

This code sample includes a global change definition that supports editing and inserting custom time zone data for a constituent address's latitude and longitude known as an Address Geocode. Included is a GlobalChangeSpec that refers to a CLR class that implements the logic for the global change processing. CLR-based specs enable you to go beyond the capabilities of stored procedures by allowing you to create a CLR class to perform the business logic. We have chosen a CLR-based implementation due to the fact that we will make calls to a Google Time Zone API. 
This sample also includes specs that define the user interface controls for the screens which allow the user to add and edit a global change instance.  


[Prerequisites/What you will build](https://www.blackbaud.com/files/support/guides/infinitydevguide/infsdk-developer-help.htm#../Subsystems/infGC-developer-help/Content/InfinityGlobalChange/coGCAddingCLR1.htm)


## Resources
* See the [Blackbaud CRM Read Me](https://github.com/blackbaud-community/Blackbaud-CRM/blob/master/README.md). 
* [Step by Step Instructions for creating this sample](https://www.blackbaud.com/files/support/guides/infinitydevguide/infsdk-developer-help.htm#../Subsystems/infGC-developer-help/Content/InfinityGlobalChange/coGCAddingCLR1.htm)
* [Global Change](https://www.blackbaud.com/files/support/guides/infinitydevguide/infsdk-developer-help.htm#../Subsystems/infGC-developer-help/Content/InfinityGlobalChange/WelcomeInfinityGlobalChange.htm) Chapter within Developer Guides
* [Administer and Configure Global Change Instances (PDF)](https://www.blackbaud.com/files/support/guides/enterprise/admin.pdf)
* [Google Maps Time Zone API](https://developers.google.com/maps/documentation/timezone/)


##Contributing##

Third-party contributions are how we keep the code samples great. We want to keep it as easy as possible to contribute changes that show others how to do cool things with Blackbaud SDKs and APIs. There are a few guidelines that we need contributors to follow.

For more information, see our [canonical contributing guide](https://github.com/blackbaud-community/Blackbaud-CRM/blob/master/CONTRIBUTING.md) in the Blackbaud CRM repo which provides detailed instructions, including signing the [Contributor License Agreement](http://developer.blackbaud.com/cla).
