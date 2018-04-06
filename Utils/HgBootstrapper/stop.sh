#!/bin/bash
cont_name="homegenie_be"
sudo docker kill --signal=SIGINT  $cont_name
