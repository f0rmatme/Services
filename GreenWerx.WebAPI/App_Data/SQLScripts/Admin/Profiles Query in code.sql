DECLARE @MEASURE			 float = 3956.55; -- miles
DECLARE @clientLat			 Decimal(9,6) = 33.369998931884766;
DECLARE @clientLon			 Decimal(9,6) = -112.37999725341797;
DECLARE @private		bit = 1; -- include private = 1
DECLARE @deleted		bit = 1; -- include deleted = 1
DECLARE @PageIndex  int = 1;
DECLARE @PageSize   int = 20;
DECLARE @parentUUID varchar(32) = '';
DECLARE @endDate Datetime = GETDATE();
DECLARE @orderBy varchar(32) = 'Distance';
DECLARE	@orderDirection varchar(32) = 'ASC';


--   GET ALL PUBLIC PROFILES

SELECT dbo.CalcDistance(@clientLat, @clientLon , p.Latitude, p.Longitude, @MEASURE ) as Distance
		, [LocationUUID]      ,[LocationType]      ,[Theme]      ,[View]      ,[UserUUID]
      ,[GUUID]      ,[GuuidType]      ,[UUID]      ,[UUIDType]      ,[UUParentID]
      ,[UUParentIDType]      ,[Name]      ,[Status]      ,[AccountUUID]      ,[Active]
      ,[Deleted]      ,[Private]      ,[SortOrder]      ,[CreatedBy]      ,[DateCreated]
      ,[Image]      ,[RoleWeight]      ,[RoleOperation]      ,[Description]      ,[LookingFor]
      ,[MembersCache]      ,[UserCache]      ,[LocationDetailCache]      ,[RelationshipStatus]
      ,[NSFW]      ,[ShowPublic]      ,[Latitude]      ,[Longitude]      ,[VerificationsCache]
  FROM [Prod_platoscom].[dbo].[Profiles] p

  WHERE 
	(p.Private = 0  ) AND
	 P.ShowPublic = 1 AND
	(p.Deleted = 0 OR p.Deleted = @deleted)    
ORDER BY Distance asc
OFFSET @PageSize * (@PageIndex - 1) ROWS
FETCH NEXT @PageSize ROWS ONLY

  


-- Get Profiles for logged in user

SELECT  dbo.CalcDistance(@clientLat, @clientLon , p.Latitude, p.Longitude, @MEASURE ) as Distance
		                , p.LocationUUID      ,p.LocationType      ,p.Theme           ,p.UserUUID
                      ,p.GUUID      ,p.GuuidType      ,p.UUID      ,p.UUIDType      ,p.UUParentID
                      ,p.UUParentIDType      ,p.Name      ,p.Status      ,p.AccountUUID      ,p.Active
                      ,p.Deleted      ,p.Private      ,p.SortOrder      ,p.CreatedBy      ,p.DateCreated
                      ,p.Image      ,p.RoleWeight      ,p.RoleOperation      ,p.Description      ,p.LookingFor
                      ,p.MembersCache      ,p.UserCache      ,p.LocationDetailCache      ,p.RelationshipStatus
                      ,p.NSFW      ,p.ShowPublic      ,p.Latitude      ,p.Longitude      ,p.VerificationsCache
                  FROM  [dbo].[Profiles] p
                  WHERE 
	                (p.Private = 0 OR p.Private = @private) AND
	                (p.Deleted = 0 OR p.Deleted = @deleted)
	 
ORDER BY Distance asc
OFFSET @PageSize * (@PageIndex - 1) ROWS
FETCH NEXT @PageSize ROWS ONLY

 -- JOIN ProfileMembers Members on p.UUID = Members.ProfileUUID AND p.AccountUUID = Members.AccountUUID 

 --       Members = string.IsNullOrWhiteSpace(s.MembersCache) ? _profileMemberManager.GetProfileMembers(s.UUID, s.AccountUUID) : JsonConvert.DeserializeObject<List<ProfileMember>>(s.MembersCache),
   --             User = string.IsNullOrWhiteSpace(s.UserCache) ? ConvertResult(userManager.Get(s.UserUUID)) : JsonConvert.DeserializeObject<User>(s.UserCache),
   --             LocationDetail = string.IsNullOrWhiteSpace(s.LocationDetailCache) ? ConvertLocationResult( locationManager.Get(s.LocationUUID) ) : JsonConvert.DeserializeObject<Location>(s.LocationDetailCache),
