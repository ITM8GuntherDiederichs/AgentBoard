@echo off
cd /d %~dp0AgentBoard
dotnet build --configuration Debug -v q
set ASPNETCORE_ENVIRONMENT=Development
dotnet exec bin\Debug\net10.0\AgentBoard.dll --urls http://localhost:5227
