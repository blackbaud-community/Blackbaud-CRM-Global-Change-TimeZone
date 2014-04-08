Imports Blackbaud.AppFx.Server
Imports System.Xml
Imports System.Data.SqlClient
Imports Blackbaud.AppFx.XmlTypes
Public NotInheritable Class AddressCoordinateTimeZoneGlobalChange
    Inherits AppCatalog.AppGlobalChangeProcess

    Public IDSetRegisterID As Guid
    Public OnlyPrimary As Boolean?
    Public OnlyChanged As Boolean?
    Private AsOf As DateTime

    Private _expectedExceptions As ExpectedDBExceptions

    Public Overrides Function ProcessGlobalChange() As AppCatalog.AppGlobalChangeResult
        Dim changeAgentID As Guid = RequestContext.GetChangeAgentID()
        Dim numberUpdated As Integer = 0
        Dim numberInserted As Integer = 0
        Dim numberExceptions As Integer = 0

        If Not MyBase.RequestArgs.LastRunOn Is Nothing Then
            AsOf = MyBase.RequestArgs.LastRunOn
        End If

        _expectedExceptions = New ExpectedDBExceptions

        Dim theConn As SqlConnection = RequestContext.OpenAppDBConnection(RequestContext.ConnectionContext.BusinessProcess)
        Dim command As SqlCommand = GetAddressCoordinateListCommand(theConn)
        Dim mergeXMLMsg As String
        mergeXMLMsg = "<ROOT>"
        Try
            Dim reader As SqlDataReader = command.ExecuteReader()
            While reader.Read()
                Dim ADDRESSCOORDINATESID As Guid = reader.GetGuid(0)
                Dim ADDRESSCOORDINATESLATITUDE As Decimal = reader.GetDecimal(1)
                Dim ADDRESSCOORDINATESLONGITUDE As Decimal = reader.GetDecimal(2)

                ' Make call to GetTimeZone function which uses an experimental Google maps time zone api
                ' The use of this service within this global change code is for educational purposes only and 
                ' is not meant for production.  
                ' This service has usage limits. 
                'https://developers.google.com/maps/documentation/timezone/
                Dim TimeZoneSvcResponse As TimeZoneResponse = GetTimeZone(ADDRESSCOORDINATESLATITUDE.ToString, ADDRESSCOORDINATESLONGITUDE.ToString)


                'Using result of calls to service and the lat and longitude, build up an xml message and pass
                'to stored procedude which will update the custom USR_ADDRESSCOORDINATETIMEZONE table
                ' Example xml message passed to stored procedure.
                '<ROOT>
                '<TimeZoneResponse ADDRESSCOORDINATESID="AEC02AB8-430B-48B8-A32C-348F98C04C2B" StatusCode="1" StatusDesc="The request was successful." TimeZoneEntryID="B6CE5E5E-236F-437E-9A42-DFDB945B5286"/>
                '<TimeZoneResponse ADDRESSCOORDINATESID="E457774D-97E4-4F0C-A35B-5020347C2EAD" StatusCode="1" StatusDesc="The request was successful." TimeZoneEntryID="B6CE5E5E-236F-437E-9A42-DFDB945B5286"/>
                '<TimeZoneResponse ADDRESSCOORDINATESID="41BDE2D6-C9F7-4AB6-86DC-129A01F98920" StatusCode="1" StatusDesc="The request was successful." TimeZoneEntryID="B6CE5E5E-236F-437E-9A42-DFDB945B5286"/>
                '<TimeZoneResponse ADDRESSCOORDINATESID="B9E1C919-811E-4B88-A39E-D8203FF08BAD" StatusCode="1" StatusDesc="The request was successful." TimeZoneEntryID="B6CE5E5E-236F-437E-9A42-DFDB945B5286"/>
                '</ROOT>

                mergeXMLMsg &= "<TimeZoneResponse ADDRESSCOORDINATESID=""" & ADDRESSCOORDINATESID.ToString & """ StatusCode=""" & TimeZoneSvcResponse.ResponseStatus & """ StatusDesc=""" & TimeZoneSvcResponse.ResponseStatusDesc.ToString & """ TimeZoneEntryID=""" & TimeZoneSvcResponse.TimeZoneEntryID.ToString & """/>"
            End While

            mergeXMLMsg &= "</ROOT>"

            reader.Close()


            Dim DBCommand As SqlCommand = New SqlCommand("USR_USP_ADDRESSCOORDINATETIMEZONE_GCPROCESS_UPDATE", theConn)
            DBCommand.CommandType = CommandType.StoredProcedure

            Dim xmlDocParm As SqlParameter = DBCommand.Parameters.Add("@doc", SqlDbType.NVarChar, -1)
            xmlDocParm.Value = mergeXMLMsg

            Dim changeAgentParm As SqlParameter = DBCommand.Parameters.Add("@CHANGEAGENTID", SqlDbType.UniqueIdentifier)
            changeAgentParm.Value = changeAgentID

            Dim myReader As SqlDataReader = DBCommand.ExecuteReader()
            Dim changeType As String

            Do While (myReader.Read())
                changeType = myReader.GetString(0)
                If changeType = "UPDATE" Then
                    numberUpdated = myReader.GetInt32(1)
                ElseIf changeType = "INSERT" Then
                    numberInserted = myReader.GetInt32(1)
                End If
            Loop

            myReader.Close()
            theConn.Close()

        Catch sqlEx As Exception
            'Build the collection of expected exceptions

            BuildExceptions("SQL Exception", sqlEx.Message)

            MyBase.HandleSQLException(sqlEx, _expectedExceptions)
        End Try

        Dim result As New AppCatalog.AppGlobalChangeResult(numberInserted, numberUpdated, 0)

        Return result
    End Function

    Private Function GetAddressCoordinateListCommand(ByVal conn As SqlConnection) As SqlCommand
        Dim baseTableName As String = "dbo.CONSTITUENT"
        Dim withClause As String = BuildRecordSecurityWithClause("CONSTITUENT", "Constituent", "ID")
        Dim FilterOutUnmodified As Boolean = OnlyChanged.HasValue AndAlso OnlyChanged.Value
        If Len(withClause) > 0 Then
            baseTableName = "CONSTITUENT_RACS"
        End If

        Dim commandText As String = withClause & _
                                   "SELECT " & _
                                   "   ADDRESSCOORDINATES.ID " & _
                                   "   , ADDRESSCOORDINATES.LATITUDE " & _
                                   "   , ADDRESSCOORDINATES.LONGITUDE  " & _
                                   "FROM " & baseTableName & " as C " & _
                                   "INNER JOIN dbo.ADDRESS on ADDRESS.CONSTITUENTID = C.ID " & _
                                   "INNER JOIN dbo.ADDRESSCOORDINATES on ADDRESSCOORDINATES.ADDRESSID = ADDRESS.ID "

        If IDSetRegisterID <> Guid.Empty Then
            Dim idSetReader As New Blackbaud.AppFx.Server.IDSetReader(IDSetRegisterID, RequestContext)
            commandText &= String.Format("inner join {0} SELECTION on SELECTION.{1} = C.ID ", idSetReader.GetResultsTableOrFunctionName(), idSetReader.IDColumnName)
        End If

        If FilterOutUnmodified Then
            commandText &= "LEFT JOIN dbo.USR_ADDRESSCOORDINATETIMEZONE on USR_ADDRESSCOORDINATETIMEZONE.ID = ADDRESSCOORDINATES.ID "
        End If

        If FilterOutUnmodified Then
            commandText &= "where (USR_ADDRESSCOORDINATETIMEZONE.ID is null or USR_ADDRESSCOORDINATETIMEZONE.DATECHANGED < ADDRESSCOORDINATES.DATECHANGED) "
        End If

        If OnlyPrimary Then
            If FilterOutUnmodified Then
                commandText &= "and ADDRESS.ISPRIMARY = 1 "
            Else
                commandText &= "where ADDRESS.ISPRIMARY = 1 "
            End If
        End If

        commandText &= "order by ADDRESSCOORDINATES.DATEADDED"

        Dim command As SqlCommand = conn.CreateCommand()
        command.CommandText = commandText
        command.CommandTimeout = 20
        command.CommandType = CommandType.Text

        Return command
    End Function

    Private Function GetTimeZone(Latitude As String, Longitude As String) As TimeZoneResponse

        Dim doc As XmlDocument
        Dim mainnode As XmlNode
        Dim Location As String = Latitude.Trim & "," & Longitude.Trim
        Dim RequestStatus As String

        ' Create a new XmlDocument  
        doc = New XmlDocument()

        'Make call to  experimental Google maps time zone api
        ' The use of this service within this global change code is for educational purposes only and 
        ' is not meant for production.  
        ' This service has usage limits. 
        'See https://developers.google.com/maps/documentation/timezone/
        ' Load data  
        doc.Load("https://maps.googleapis.com/maps/api/timezone/xml?location=" & Location & "&timestamp=0&sensor=false")

        mainnode = doc.SelectSingleNode("TimeZoneResponse")
        RequestStatus = mainnode.SelectSingleNode("status").InnerText

        If mainnode Is Nothing Then
            Return Nothing

        ElseIf RequestStatus = "OK" Then
            Dim timezonename As String = mainnode.SelectSingleNode("time_zone_name").InnerText
            Dim TimeZoneGuid As System.Guid = GetTimeZoneEntryID(timezonename)

            If TimeZoneGuid = System.Guid.Empty Then
                Return New TimeZoneResponse(TimeZoneResponse.Status.TIMEZONEENTRY_FK_NOTFOUND, "indicates no row found in TIMEZONEENTRY table in DB for '" & timezonename & "'.", "", System.Guid.Empty)
            Else
                Return New TimeZoneResponse(TimeZoneResponse.Status.OK, "The request was successful.", timezonename, TimeZoneGuid)
            End If

        ElseIf RequestStatus = "INVALID_REQUEST" Then
            Return New TimeZoneResponse(TimeZoneResponse.Status.INVALID_REQUEST, "The API request was malformed.", "", System.Guid.Empty)
        ElseIf RequestStatus = "OVER_QUERY_LIMIT" Then
            Return New TimeZoneResponse(TimeZoneResponse.Status.OVER_QUERY_LIMIT, "The requestor has exceeded API quota.", "", System.Guid.Empty)
        ElseIf RequestStatus = "REQUEST_DENIED" Then
            Return New TimeZoneResponse(TimeZoneResponse.Status.REQUEST_DENIED, "The API did not complete the request. Confirm that the request was sent over http instead of https.", "", System.Guid.Empty)
        ElseIf RequestStatus = "UNKNOWN_ERROR" Then
            Return New TimeZoneResponse(TimeZoneResponse.Status.UNKNOWN_ERROR, "indicates an unknown API error.", "", System.Guid.Empty)
        ElseIf RequestStatus = "ZERO_RESULTS" Then
            Return New TimeZoneResponse(TimeZoneResponse.Status.ZERO_RESULTS, "indicates that no time zone API data could be found for the specified position or time. Confirm that the request is for a location on land, and not over water.", "", System.Guid.Empty)
        Else
            Return New TimeZoneResponse(TimeZoneResponse.Status.GENERAL_ERROR, "indicates a general error.", "", System.Guid.Empty)
        End If
       
    End Function

    Private Function GetTimeZoneEntryID(TimeZoneEntry As String) As System.Guid

        Dim DBConn As SqlConnection = RequestContext.OpenAppDBConnection(RequestContext.ConnectionContext.BusinessProcess)
        Dim DBCommand As SqlCommand = New SqlCommand("USR_USP_TIMEZONEENTRY_GETBYNAME", DBConn)

        DBCommand.CommandType = CommandType.StoredProcedure

        Dim myParm As SqlParameter = DBCommand.Parameters.Add("@TimeZoneName", SqlDbType.NVarChar, 400)

        myParm.Value = TimeZoneEntry

        Dim myReader As SqlDataReader
        Dim retval As System.Guid
        Try
            myReader = DBCommand.ExecuteReader()

            'EXEC(USR_USP_TIMEZONEENTRY_GETBYNAME) 'Eastern Standard Time'

            Do While (myReader.Read())

                If myReader.IsDBNull(0) Then
                    retval = System.Guid.Empty
                Else
                    Console.WriteLine("{0}", myReader.GetGuid(0))
                    retval = myReader.GetGuid(0)
                End If

            Loop
        Catch ex As Exception
            Throw ex
        Finally
            myReader.Close()
            DBConn.Close()
        End Try

        myReader.Close()
        DBConn.Close()
        Return retval

    End Function

    Protected Class TimeZoneResponse
        Public Enum Status As Integer
            OK = 1
            INVALID_REQUEST = 2
            OVER_QUERY_LIMIT = 3
            REQUEST_DENIED = 4
            UNKNOWN_ERROR = 5
            ZERO_RESULTS = 6
            GENERAL_ERROR = 7
            TIMEZONEENTRY_FK_NOTFOUND = 8
        End Enum

        Private _responseStatus As Status
        Private _responseStatusDesc As String
        Private _timeZone As String
        Private _timeZoneEntryID As Guid

        Sub New(ByVal responseStatus As Status, ByVal responseStatusDesc As String, ByVal timeZone As String, timeZoneEntryID As Guid)
            _responseStatus = responseStatus
            _responseStatusDesc = responseStatusDesc
            _timeZone = timeZone
            _timeZoneEntryID = timeZoneEntryID
        End Sub

        Public ReadOnly Property ResponseStatus As Status
            Get
                Return _responseStatus
            End Get
        End Property

        Public ReadOnly Property ResponseStatusDesc As String
            Get
                Return _responseStatusDesc
            End Get
        End Property

        Public ReadOnly Property TimeZone As String
            Get
                Return _timeZone
            End Get
        End Property

        Public ReadOnly Property TimeZoneEntryID As Guid
            Get
                Return _timeZoneEntryID
            End Get
        End Property

    End Class

    Private Sub BuildExceptions(ByVal SearchText As String, ByVal CustomErrorMsg As String)
        _expectedExceptions = New ExpectedDBExceptions
        ReDim _expectedExceptions.CustomExceptions(0)

        Dim invalidSelection As New CustomExceptionDescriptor
        invalidSelection.SearchText = SearchText
        invalidSelection.CustomErrorMsg = CustomErrorMsg

        _expectedExceptions.CustomExceptions(0) = invalidSelection
    End Sub
End Class

