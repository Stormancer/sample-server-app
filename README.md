# sample-server-app
A simple Stormancer server application demonstrating auth, party, gamefinder and P2P gamesession capabilities


#installing
    
    # Install the Stormancer CLI
    dotnet tools install Stormancer.CLI --global
    
    # Install the Management CLI.
    stormancer plugins install --id Stormancer.Management.CLI
    
    # Create default server config
    stormancer new config
    # Start server and return
    stormancer start &
    
    # Add the local cluster that was just started to the list of managed clusters.
    stormancer manage clusters add --endpoint http://localhost:81
    
    # Create an application profile that contain all the info required to deploy an application.
    stormancer new app-profile --cluster default --account tests --app test -o test-app.json --configSource https://raw.githubusercontent.com/Stormancer/sample-server-app/dev/config.json --deploySource git|https://github.com/Stormancer/sample-server-app.git|dev|src
    
    # Creates and deploy the app described in the app profile. (-a = create the app in the cluster if it doesn't exist, -c = update the app configuration from the configSource, -d = deploy from the deploySource) 
    stormancer manage deploy --profile test-app.json -a -c -d
