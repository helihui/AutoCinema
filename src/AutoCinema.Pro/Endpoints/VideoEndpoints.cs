using AutoCinema.Pro.Models;
using AutoCinema.Pro.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AutoCinema.Pro.Endpoints;

public static class VideoEndpoints
{
    public static void MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        // 旧的单任务 API 已废弃，请使用 JobController 相关接口
    }
}
