@echo off
cd /d %~dp0AgentBoard
set ASPNETCORE_ENVIRONMENT=Development
dotnet run --urls http://localhost:5227
