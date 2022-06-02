#!/bin/bash

wget -c https://coqui.gateway.scarf.sh/english/coqui/v1.0.0-huge-vocab/model.tflite -O ./TranscripterUI/model/english_huge_1.0.0_model.tflite
wget -c https://coqui.gateway.scarf.sh/english/coqui/v1.0.0-huge-vocab/huge-vocabulary.scorer -O ./TranscripterUI/model/huge-vocabulary.scorer 
