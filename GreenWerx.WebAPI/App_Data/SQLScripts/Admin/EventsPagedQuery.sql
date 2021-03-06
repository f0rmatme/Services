
declare @PARENTUUID varchar(32), @PRIVATE bit, @DELETED bit, @ENDDATE DateTime, @PAGESIZE int, @PAGEINDEX int, @CLIENTLAT float, @CLIENTLON float, @MEASURE FLOAT;
SET @PAGESIZE = 100;
SET @PAGEINDEX = 1;
SET @DELETED = 0;
SET @ENDDATE = '2020-3-4 16:00:00';
SET @PARENTUUID = '';
SET @PRIVATE = 1;
SET @CLIENTLAT =  33.369998931884766;
SET @CLIENTLON =  -112.37999725341797;
SET @MEASURE = 3956.55;


--SELECT  --  CalcDistance(@CLIENTLAT, @CLIENTLON , e.Latitude, e.Longitude, @MEASURE )  as Distance,
--                                            [Name]	,[Category]	,[EventDateTime]	,[RepeatCount]
--		                                    ,[RepeatForever]	,[Frequency]		,[StartDate]	,[EndDate]
--		                                    ,[Url]				,[HostAccountUUID]	,[GUUID]		,[GuuidType]
--		                                    ,[UUID]				,[UUIDType]			,[UUParentID]   ,[UUParentIDType]
--		                                    ,[Status]			,[AccountUUID]      ,[Active]		,[Deleted]
--		                                    ,[Private]			,[SortOrder]		,[CreatedBy]    ,[DateCreated]
--		                                    ,[Image]			,[RoleWeight]		,[RoleOperation],[NSFW]
--		                                    ,[Latitude]			,[Longitude] 		,[Description]
--                                    FROM Events e
--                                   WHERE 
--	                                (e.UUID = @PARENTUUID OR e.UUParentID =  @PARENTUUID ) AND
--	                                (e.Private = 0 OR e.Private = @PRIVATE) AND
--	                                (e.Deleted = 0 OR e.Deleted = @DELETED) AND
--	                                (e.EndDate > @ENDDATE) ORDER BY startdate ASC OFFSET @PAGESIZE *(@PAGEINDEX - 1) ROWS FETCH NEXT @PAGESIZE ROWS ONLY

	
	SELECT CEILING(dbo.CalcDistance(@CLIENTLAT, @CLIENTLON , el.Latitude, el.Longitude, @MEASURE ) ) as Distance
                                            ,a.Name AS HostName
                                            ,el.Name AS Location
											,el.City
											,el.State
											,el.Country
                                            ,e.[Name]	            ,e.[Category]	        ,e.[EventDateTime],e.[RepeatCount]
		                                    ,e.[RepeatForever]	,e.[Frequency]		,e.[StartDate]	,e.[EndDate]
		                                    ,e.[Url]				,e.[HostAccountUUID]	,e.[GUUID]		,e.[GuuidType]
		                                    ,e.[UUID]				,e.[UUIDType]			,e.[UUParentID]   ,e.[UUParentIDType]
		                                    ,e.[Status]			,e.[AccountUUID]      ,e.[Active]		,e.[Deleted]
		                                    ,e.[Private]			,e.[SortOrder]		,e.[CreatedBy]    ,e.[DateCreated]
		                                    ,e.[Image]			,e.[RoleWeight]		,e.[RoleOperation],e.[NSFW]
		                                    ,e.[Latitude]			,e.[Longitude] 		,e.[Description], e.[IsAffiliate]
                                            ,el.Latitude, el.Longitude
                                    FROM Events e
                                    LEFT JOIN Accounts a ON e.HostAccountUUID = a.UUID
                                    LEFT JOIN (SELECT DISTINCT EventUUID,Latitude, Longitude, Name, City, State, Country FROM EventLocations) el   ON e.UUID = el.EventUUID
                                   WHERE
	                                --(e.UUID = @PARENTUUID OR e.UUParentID =  @PARENTUUID ) AND
	                                (
									e.Private =0
									 OR 
									e.Private =  @PRIVATE
									) 
									AND
	                                (e.Deleted = 0 OR e.Deleted = @DELETED) 
									AND
	                                (e.EndDate > @ENDDATE) ORDER BY startdate ASC OFFSET @PAGESIZE *(@PAGEINDEX - 1) ROWS FETCH NEXT @PAGESIZE ROWS ONLY								 