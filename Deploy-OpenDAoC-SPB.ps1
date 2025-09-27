docker build --no-cache -t opendaoc-singleplayerbots:latest .
docker tag opendaoc-singleplayerbots:latest localhost:5000/opendaoc-singleplayerbots:latest
docker push localhost:5000/opendaoc-singleplayerbots:latest
docker compose pull
docker compose up -d