build:
	dotnet build
start:
	dotnet run --project src/Host/Host.csproj
watch:
	dotnet watch --project src/Host/Host.csproj run
nuget:
	nuget pack -NoDefaultExcludes -OutputDirectory nupkg
publish:
	dotnet publish --os linux --arch x64 -c Release --self-contained
publish-to-hub:
	dotnet publish --os linux --arch x64 -c Release -p:ContainerRegistry=docker.io -p:ContainerImageName=phamquangvinh/drdentist-net-api --self-contained
tp: # terraform plan
	cd terraform/environments/staging && terraform plan
ta: # terraform apply
	cd terraform/environments/staging && terraform apply
td: # terraform destroy
	cd terraform/environments/staging && terraform destroy
dcu: # docker-compose up : webapi + postgresql
	cd docker-compose/ && docker-compose -f docker-compose.postgresql.yml up -d
dcd: # docker-compose down : webapi + postgresql
	cd docker-compose/ && docker-compose -f docker-compose.postgresql.yml down
fds: # force redeploy aws ecs service
	aws ecs update-service --force-new-deployment --service dotnet-webapi --cluster fullstackhero
gw: # git docker workflow to push docker image to the repository based on the main branch
	@echo triggering github workflow to push docker image to container
	@echo ensure that you have the gh-cli installed and authenticated.
	gh workflow run dotnet-cicd -f push_to_docker=true
gw: # git docker workflow to push docker image to the repository based on the main branch
	@echo triggering github workflow to push docker image to container
	@echo ensure that you have the gh-cli installed and authenticated.
	gh workflow run dotnet-cicd -f push_to_docker=true
deploy: # deploy to docker
	@echo building images
	dotnet build --no-restore
	@echo publishing images
	dotnet publish src/Host/Host.csproj -c Release -p:ContainerImageName=phamquangvinh/drdentist-net-api -p:RuntimeIdentifier=linux-x64
	@echo pushing images
	docker push phamquangvinh/drdentist-net-api --all-tags