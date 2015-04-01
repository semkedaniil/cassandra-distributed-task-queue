using System.Collections.Generic;
using System.Linq;

using Elasticsearch.Net;

using SKBKontur.Catalogue.Core.ElasticsearchClientExtensions;
using SKBKontur.Catalogue.Core.ElasticsearchClientExtensions.Responses.Search;

namespace SKBKontur.Catalogue.RemoteTaskQueue.ElasticMonitoring.TaskIndexedStorage.Client
{
    public class TaskSearchClient : ITaskSearchClient
    {
        public TaskSearchClient(IElasticsearchClientFactory elasticsearchClientFactory)
        {
            elasticsearchClient = elasticsearchClientFactory.GetClient();
        }

        private static bool NotEmpty(string[] arr)
        {
            return arr != null && arr.Length > 0;
        }

        public TaskSearchResponse SearchNext(string scrollId)
        {
            var result = elasticsearchClient.Scroll<SearchResponseNoData>(scrollId, null, x => x.AddQueryString("scroll", scrollLiveTime)).ProcessResponse();
            return new TaskSearchResponse
                {
                    Ids = result.Response.Hits.Hits.Select(x => x.Id).ToArray(),
                    TotalCount = result.Response.Hits.Total,
                    NextScrollId = result.Response.ScrollId
                };
        }

        public TaskSearchResponse SearchFirst(TaskSearchRequest taskSearchRequest)
        {
            var filters = new List<object>
                {
                    new
                        {
                            query_string = new
                                {
                                    query = taskSearchRequest.QueryString
                                },
                        }
                };
            if(NotEmpty(taskSearchRequest.TaskNames))
            {
                filters.Add(new
                    {
                        terms = new Dictionary<string, object>
                            {
                                {"Meta.Name", taskSearchRequest.TaskNames},
                                {"minimum_should_match", 1}
                            }
                    });
            }
            if(NotEmpty(taskSearchRequest.TaskStates))
            {
                filters.Add(new
                    {
                        terms = new Dictionary<string, object>
                            {
                                {"Meta.State", taskSearchRequest.TaskStates},
                                {"minimum_should_match", 1}
                            }
                    });
            }

            var metaResponse =
                elasticsearchClient
                    .Search<SearchResponseNoData>(IndexNameFactory.GetIndexForTimeRange(taskSearchRequest.FromTicksUtc, taskSearchRequest.ToTicksUtc), new
                        {
                            size = pageSize,
                            version = true,
                            _source = false,
                            query = new
                                {
                                    @bool = new
                                        {
                                            must = filters
                                        }
                                },
                            sort = new[]
                                {
                                    new Dictionary<string, object>
                                        {
                                            {"Meta.MinimalStartTime", new {order = "desc"}}
                                        }
                                }
                        }, x => x.IgnoreUnavailable(true).Scroll(scrollLiveTime).SearchType(SearchType.QueryThenFetch))
                    .ProcessResponse();
            return new TaskSearchResponse
                {
                    Ids = metaResponse.Response.Hits.Hits.Select(x => x.Id).ToArray(),
                    TotalCount = metaResponse.Response.Hits.Total,
                    NextScrollId = metaResponse.Response.ScrollId
                };
        }

        private const string scrollLiveTime = "10m";
        private const int pageSize = 100;
        private readonly IElasticsearchClient elasticsearchClient;
    }
}