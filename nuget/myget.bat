nuget pack Transformalize.Provider.MySql.nuspec -OutputDirectory "c:\temp\modules"
nuget pack Transformalize.Provider.MySql.Autofac.nuspec -OutputDirectory "c:\temp\modules"

REM nuget push "c:\temp\modules\Transformalize.Provider.MySql.0.7.7-beta.nupkg" -source https://www.myget.org/F/transformalize/api/v3/index.json
REM nuget push "c:\temp\modules\Transformalize.Provider.MySql.Autofac.0.7.7-beta.nupkg" -source https://www.myget.org/F/transformalize/api/v3/index.json






