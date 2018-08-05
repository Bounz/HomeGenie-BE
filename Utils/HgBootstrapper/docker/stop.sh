#!/bin/bash
cont_name="homegenie_be"
docker kill --signal=SIGINT  $cont_name
