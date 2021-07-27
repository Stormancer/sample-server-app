// StressTool.cpp : Ce fichier contient la fonction 'main'. L'exécution du programme commence et se termine à cet endroit.
//
#define NOMINMAX
#include <iostream>
#include "Worker.h"
#include "Timer.h"


struct Stats
{
    double avg;
    double max;
    double min;
    double successRate;
};
Stats stats(std::vector<StressTool::Result> results)
{
    double acc = 0;
    double count = 0;
    double min = std::numeric_limits<double>::max();
    double max = std::numeric_limits<double>::min();
    
    for (auto v : results)
    {
        if (v.success)
        {

            acc += v.duration;
            count++;
            if (min > v.duration)
            {
                min = v.duration;
            }
            if (max < v.duration)
            {
                max = v.duration;
            }
        }
    }
    Stats stats;
    stats.avg = acc / count;
    stats.max = max;
    stats.min = min;
    stats.successRate = count / results.size();
    return stats;
}
int main()
{
    for (int l=0; l < 1000; l++)
    {
        int concurrentWorkers = 10;

        std::vector<pplx::task<StressTool::Result>> tasks;

        Timer timer;
        timer.start();
        
        for (int i = 0; i < concurrentWorkers; i++)
        {
            
            auto result = pplx::create_task([i]() {
                StressTool::ConnectionWorker worker;
                return worker.run(i); 
                });

            tasks.push_back(result);

        }
        timer.stop();
        std::cout << "startup time : " << timer.getElapsedTimeInMilliSec() << "ms\n";
        timer.start();
        auto results = pplx::when_all(tasks.begin(), tasks.end()).get();
        timer.stop();
        std::cout << "execution time : " << timer.getElapsedTimeInMilliSec() << "ms\n";
        auto result = stats(results);

        std::cout << "success rate : " << result.successRate * 100 << "%\n";
        std::cout << "avg          : " << result.avg << "ms\n";
        std::cout << "min          : " << result.min << "ms\n";
        std::cout << "max          : " << result.max << "ms\n";
       
      
    }
    std::string _;
    std::getline(std::cin, _);
}

// Exécuter le programme : Ctrl+F5 ou menu Déboguer > Exécuter sans débogage
// Déboguer le programme : F5 ou menu Déboguer > Démarrer le débogage

// Astuces pour bien démarrer : 
//   1. Utilisez la fenêtre Explorateur de solutions pour ajouter des fichiers et les gérer.
//   2. Utilisez la fenêtre Team Explorer pour vous connecter au contrôle de code source.
//   3. Utilisez la fenêtre Sortie pour voir la sortie de la génération et d'autres messages.
//   4. Utilisez la fenêtre Liste d'erreurs pour voir les erreurs.
//   5. Accédez à Projet > Ajouter un nouvel élément pour créer des fichiers de code, ou à Projet > Ajouter un élément existant pour ajouter des fichiers de code existants au projet.
//   6. Pour rouvrir ce projet plus tard, accédez à Fichier > Ouvrir > Projet et sélectionnez le fichier .sln.
