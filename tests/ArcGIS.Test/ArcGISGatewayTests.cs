﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.ServiceModel.Common;
using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Operation;
using Xunit;
using ServiceStack.Text;
using ArcGIS.ServiceModel.Serializers;
using System.Threading;

namespace ArcGIS.Test
{
    public class ArcGISGateway : PortalGateway
    {
        public ArcGISGateway(ISerializer serializer = null)
            : this(@"http://sampleserver3.arcgisonline.com/ArcGIS/", null, serializer)
        { }

        public ArcGISGateway(string root, ITokenProvider tokenProvider, ISerializer serializer = null)
            : base(root, serializer, tokenProvider)
        {
        }

        public Task<QueryResponse<T>> QueryAsPost<T>(Query queryOptions) where T : IGeometry
        {
            return Post<QueryResponse<T>, Query>(queryOptions, CancellationToken.None);
        }

        public Task<AgsObject> GetAnything(ArcGISServerEndpoint endpoint)
        {
            return Get<AgsObject>(endpoint, CancellationToken.None);
        }

        internal readonly static Dictionary<string, Func<Type>> TypeMap = new Dictionary<string, Func<Type>>
            {
                { GeometryTypes.Point, () => typeof(Point) },
                { GeometryTypes.MultiPoint, () => typeof(MultiPoint) },
                { GeometryTypes.Envelope, () => typeof(Extent) },
                { GeometryTypes.Polygon, () => typeof(Polygon) },
                { GeometryTypes.Polyline, () => typeof(Polyline) }
            };

        public async Task<FindResponse> DoFind(Find findOptions)
        {
            var response = await Find(findOptions);

            foreach (var result in response.Results.Where(r => r.Geometry != null))
            {
                result.Geometry = ServiceStack.Text.JsonSerializer.DeserializeFromString(result.Geometry.SerializeToString(), TypeMap[result.GeometryType]());
            }
            return response;
        }
    }

    public class AgsObject : JsonObject, IPortalResponse
    {
        [System.Runtime.Serialization.DataMember(Name = "error")]
        public ArcGISError Error { get; set; }

        [System.Runtime.Serialization.DataMember(Name = "links")]
        public List<Link> Links { get; set; }
    }

    public class QueryGateway : PortalGateway
    {
        public QueryGateway(ISerializer serializer = null)
            : base(@"http://services.arcgisonline.com/arcgis/", serializer)
        { }
    }

    public class ArcGISGatewayTests : TestsFixture
    {
        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/", "/Earthquakes/EarthquakesFromLastSevenDays/MapServer")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis", "Generic/newzealand/MapServer")]
        public async Task CanGetAnythingFromServer(string rootUrl, string relativeUrl)
        {
            var gateway = new ArcGISGateway(rootUrl, null);
            var endpoint = new ArcGISServerEndpoint(relativeUrl);

            var response = await gateway.GetAnything(endpoint);

            Assert.Null(response.Error);
            Assert.True(response.ContainsKey("capabilities"));
            Assert.True(response.ContainsKey("mapName"));
            Assert.True(response.ContainsKey("layers"));
            Assert.True(response.ContainsKey("documentInfo"));
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis")]
        public async Task CanPingServer(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var endpoint = new ArcGISServerEndpoint("/");

            var response = await gateway.Ping(endpoint);

            Assert.Null(response.Error);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis")]
        public async Task CanDescribeSite(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var response = await gateway.DescribeSite();

            Assert.NotNull(response);
            Assert.True(response.Version > 0);

            foreach (var resource in response.ArcGISServerEndpoints)
            {
                var ping = await gateway.Ping(resource);
                Assert.Null(ping.Error);
            }
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis/rest/")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis/rest/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis/rest")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis/rest")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis/")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis")]
        public void GatewayRootUrlHasCorrectFormat(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            Assert.True(gateway.RootUrl.EndsWith("/"));
            Assert.True(gateway.RootUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) || gateway.RootUrl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase));
            Assert.False(gateway.RootUrl.ToLowerInvariant().Contains("/rest/services/"));
        }

        [Fact]
        public void EmptyRootUrlThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => "".AsRootUrl());
        }

        [Theory]
        [InlineData("http://www.arcgis.com/arcgis")]
        [InlineData("http://www.arcgis.com/ArcGIS")]
        [InlineData("http://www.arcgis.com/ArcGIS/")]
        [InlineData("http://www.arcgis.com/ArcGIS/")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/services")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/services/")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/services/rest/services")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/services/rest/services/")]
        [InlineData("http://www.arcgis.com/ArcGIS/admin")]
        [InlineData("http://www.arcgis.com/ArcGIS/admin/")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/admin/services")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/admin/services/")]
        [InlineData("http://www.arcgis.com/ArcGIS/rest/ADMIN/services/")]
        public void HttpRootUrlHasCorrectFormat(string urlToTest)
        {
            var rootUrl = urlToTest.AsRootUrl();
            Assert.Equal("http://www.arcgis.com/arcgis/", rootUrl, true);
        }

        [Theory]
        [InlineData("https://www.arcgis.com/arcgis")]
        [InlineData("https://www.arcgis.com/ArcGIS")]
        [InlineData("https://www.arcgis.com/ArcGIS/")]
        [InlineData("https://www.arcgis.com/ArcGIS/")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/services")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/services/")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/services/rest/services")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/services/rest/services/")]
        [InlineData("https://www.arcgis.com/ArcGIS/admin")]
        [InlineData("https://www.arcgis.com/ArcGIS/admin/")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/admin/services")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/admin/services/")]
        [InlineData("https://www.arcgis.com/ArcGIS/rest/ADMIN/services/")]
        public void HttpsRootUrlHasCorrectFormat(string urlToTest)
        {
            var rootUrl = urlToTest.AsRootUrl();
            Assert.Equal("https://www.arcgis.com/arcgis/", rootUrl, true);
        }

        [Fact]
        public async Task GatewayDoesAutoPost()
        {
            var gateway = new ArcGISGateway() { IncludeHypermediaWithResponse = true };

            var longWhere = new StringBuilder("region = '");
            for (var i = 0; i < 3000; i++)
                longWhere.Append(i);

            var query = new Query(@"/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { Where = longWhere + "'" };

            var result = await gateway.Query<Point>(query);
            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.SpatialReference);
            Assert.False(result.Features.Any());
            Assert.NotNull(result.Links);
            Assert.Equal("POST", result.Links.First().Method);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS", "/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0")]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS", "Earthquakes/Since_1970/MapServer/0")]
        public async Task QueryCanReturnPointFeatures(string rootUrl, string relativeUrl)
        {
            var gateway = new PortalGateway(rootUrl);

            var query = new Query(relativeUrl.AsEndpoint());
            var result = await gateway.Query<Point>(query);

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.SpatialReference);
            Assert.True(result.Features.Any());
            Assert.Null(result.Links);
            Assert.True(result.Features.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task QueryCanReturnDifferentGeometryTypes()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { Where = "magnitude > 4.5" };
            var resultPoint = await gateway.QueryAsPost<Point>(queryPoint);

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry != null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" } };
            var resultPolyline = await gateway.Query<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry != null));

            gateway = new ArcGISGateway(new JsonDotNetSerializer());

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint()) { Where = "areasqkm = 0.012", OutFields = new List<string> { "areasqkm" } };
            var resultPolygon = await gateway.QueryAsPost<Polygon>(queryPolygon);

            Assert.True(resultPolygon.Features.Any());
            Assert.True(resultPolygon.Features.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task QueryCanReturnNoGeometry()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { ReturnGeometry = false };
            var resultPoint = await gateway.Query<Point>(queryPoint);

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" }, ReturnGeometry = false };
            var resultPolyline = await gateway.QueryAsPost<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry == null));
        }

        [Fact]
        public async Task QueryCanUseWhereClause()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/Since_1970/MapServer/0".AsEndpoint())
            {
                ReturnGeometry = false,
                Where = "UPPER(Name) LIKE UPPER('New Zea%')"
            };
            queryPoint.OutFields.Add("Name");
            var resultPoint = await gateway.Query<Point>(queryPoint);

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));
            Assert.True(resultPoint.Features.All(i => i.Attributes["Name"].ToString().StartsWithIgnoreCase("New Zea")));
        }

        [Fact]
        public async Task QueryObjectIdsAreHonored()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { ReturnGeometry = false };
            var resultPoint = await gateway.Query<Point>(queryPoint);

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));

            var queryPointByOID = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint())
            {
                ReturnGeometry = false,
                ObjectIds = resultPoint.Features.Take(10).Select(f => long.Parse(f.Attributes["objectid"].ToString())).ToList()
            };
            var resultPointByOID = await gateway.Query<Point>(queryPointByOID);

            Assert.True(resultPointByOID.Features.Any());
            Assert.True(resultPointByOID.Features.All(i => i.Geometry == null));
            Assert.True(resultPoint.Features.Count() > 10);
            Assert.True(resultPointByOID.Features.Count() == 10);
            Assert.False(queryPointByOID.ObjectIds.Except(resultPointByOID.Features.Select(f => f.ObjectID)).Any());
        }

        [Fact]
        public async Task QueryOutFieldsAreHonored()
        {
            var gateway = new ArcGISGateway();

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" }, ReturnGeometry = false };
            var resultPolyline = await gateway.Query<Polyline>(queryPolyline);

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry == null));
            Assert.True(resultPolyline.Features.All(i => i.Attributes != null && i.Attributes.Count == 1));

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint())
            {
                Where = "areasqkm = 0.012",
                OutFields = new List<string> { "areasqkm", "elevation", "resolution", "reachcode" }
            };
            var resultPolygon = await gateway.Query<Polygon>(queryPolygon);

            Assert.True(resultPolygon.Features.Any());
            Assert.True(resultPolygon.Features.All(i => i.Geometry != null));
            Assert.True(resultPolygon.Features.All(i => i.Attributes != null && i.Attributes.Count == 4));
        }

        [Fact]
        public async Task CanQueryForCount()
        {
            var gateway = new QueryGateway();

            var query = new QueryForCount(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint());
            var result = await gateway.QueryForCount(query);

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.NumberOfResults > 0);
        }

        [Fact]
        public async Task CanQueryForIds()
        {
            var gateway = new QueryGateway();

            var query = new QueryForIds(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint());
            var result = await gateway.QueryForIds(query);

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.ObjectIds);
            Assert.True(result.ObjectIds.Any());

            var queryFiltered = new QueryForIds(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint())
            {
                ObjectIds = result.ObjectIds.Take(100).ToList()
            };
            var resultFiltered = await gateway.QueryForIds(queryFiltered);

            Assert.NotNull(resultFiltered);
            Assert.Null(resultFiltered.Error);
            Assert.NotNull(resultFiltered.ObjectIds);
            Assert.True(resultFiltered.ObjectIds.Any());
            Assert.True(resultFiltered.ObjectIds.Count() == queryFiltered.ObjectIds.Count);
        }

        /// <summary>
        /// Performs unfiltered query, then filters by Extent and Polygon to SE quadrant of globe and verifies both filtered
        /// results contain same number of features as each other, and that both filtered resultsets contain fewer features than unfiltered resultset.
        /// </summary>
        /// <param name="serviceUrl"></param>
        /// <returns></returns>
        public async Task QueryGeometryCriteriaHonored(string serviceUrl)
        {
            int countAllResults = 0;
            int countExtentResults = 0;
            int countPolygonResults = 0;

            var gateway = new ArcGISGateway(new JsonDotNetSerializer());

            var queryPointAllResults = new Query(serviceUrl.AsEndpoint());

            var resultPointAllResults = await gateway.Query<Point>(queryPointAllResults);

            var queryPointExtentResults = new Query(serviceUrl.AsEndpoint())
            {
                Geometry = new Extent { XMin = 0, YMin = 0, XMax = 180, YMax = -90, SpatialReference = SpatialReference.WGS84 }, // SE quarter of globe
                OutputSpatialReference = SpatialReference.WebMercator
            };
            var resultPointExtentResults = await gateway.Query<Point>(queryPointExtentResults);

            var rings = new Point[] 
            { 
                new Point { X = 0, Y = 0 }, 
                new Point { X = 180, Y = 0 }, 
                new Point { X = 180, Y = -90 }, 
                new Point { X = 0, Y = -90 }, 
                new Point { X = 0, Y = 0 }
            }.ToPointCollectionList();

            var queryPointPolygonResults = new Query(serviceUrl.AsEndpoint())
            {
                Geometry = new Polygon { Rings = rings }
            };
            var resultPointPolygonResults = await gateway.Query<Point>(queryPointPolygonResults);

            countAllResults = resultPointAllResults.Features.Count();
            countExtentResults = resultPointExtentResults.Features.Count();
            countPolygonResults = resultPointPolygonResults.Features.Count();

            Assert.Equal(resultPointExtentResults.SpatialReference.Wkid, queryPointExtentResults.OutputSpatialReference.LatestWkid);
            Assert.True(countAllResults > countExtentResults);
            Assert.True(countPolygonResults == countExtentResults);
        }

        /// <summary>
        /// Test geometry query against MapServer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task QueryMapServerGeometryCriteriaHonored()
        {
            await QueryGeometryCriteriaHonored(@"/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0");
        }

        /// <summary>
        /// Test geometry query against FeatureServer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task QueryFeatureServerGeometryCriteriaHonored()
        {
            await QueryGeometryCriteriaHonored(@"/Earthquakes/EarthquakesFromLastSevenDays/FeatureServer/0");
        }

        [Fact]
        public async Task CanAddUpdateAndDelete()
        {
            var gateway = new ArcGISGateway();

            var feature = new Feature<Point>();
            feature.Attributes.Add("type", 0);
            feature.Geometry = new Point { SpatialReference = new SpatialReference { Wkid = SpatialReference.WebMercator.Wkid }, X = -13073617.8735768, Y = 4071422.42978062 };

            var adds = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Adds = new List<Feature<Point>> { feature }
            };
            var resultAdd = await gateway.ApplyEdits(adds);

            Assert.True(resultAdd.Adds.Any());
            Assert.True(resultAdd.Adds.First().Success);

            var id = resultAdd.Adds.First().ObjectId;

            feature.Attributes.Add("description", "'something'"); // problem with serialization means we need single quotes around string values
            feature.Attributes.Add("objectId", id);

            var updates = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Updates = new List<Feature<Point>> { feature }
            };
            var resultUpdate = await gateway.ApplyEdits(updates);

            Assert.True(resultUpdate.Updates.Any());
            Assert.True(resultUpdate.Updates.First().Success);
            Assert.Equal(resultUpdate.Updates.First().ObjectId, id);

            var deletes = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Deletes = new List<long> { id }
            };
            var resultDelete = await gateway.ApplyEdits(deletes);

            Assert.True(resultDelete.Deletes.Any());
            Assert.True(resultDelete.Deletes.First().Success);
            Assert.Equal(resultDelete.Deletes.First().ObjectId, id);
        }

        [Fact]
        public async Task FindCanReturnResultsAndGeometry()
        {
            var gateway = new ArcGISGateway();

            var find = new Find(@"/Network/USA/MapServer".AsEndpoint())
            {
                SearchText = "route",
                LayerIdsToSearch = new List<int> { 1, 2, 3 },
                SearchFields = new List<string> { "Name", "RouteName" }
            };
            var result = await gateway.Find(find);

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.Results.Any());
            Assert.True(result.Results.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task FindCanReturnResultsAndNoGeometry()
        {
            var gateway = new ArcGISGateway();

            var find = new Find(@"/Network/USA/MapServer".AsEndpoint())
            {
                SearchText = "route",
                LayerIdsToSearch = new List<int> { 1, 2, 3 },
                SearchFields = new List<string> { "Name", "RouteName" },
                ReturnGeometry = false
            };
            var result = await gateway.Find(find);

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.Results.Any());
            Assert.True(result.Results.All(i => i.Geometry == null));
        }
    }
}
