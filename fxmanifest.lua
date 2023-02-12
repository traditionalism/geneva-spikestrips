fx_version 'cerulean'
game 'gta5'

author 'traditionalism/geneva'
description 'A simple standalone spikestrip resource made in C#.'

dependency '/onesync'

file 'Client/bin/Release/**/publish/*.dll'

client_script 'Client/bin/Release/**/publish/*.net.dll'
server_script 'Server/bin/Release/**/publish/*.net.dll'

deploy_time '3' -- time to deploy spikestrips in seconds
retract_time '2' -- time to retract spikestrips in seconds
min_spikes '2' -- minimum spikes placed at once
max_spikes '4' -- maximum spikes placed at once
