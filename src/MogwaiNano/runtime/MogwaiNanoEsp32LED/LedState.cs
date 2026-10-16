// Copyright 2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace MogwaiNanoEsp32LED
{
    /// <summary>
    /// États du runtime affichés par la LED intégrée.
    /// </summary>
    public enum LedState
    {
        /// <summary>LED éteinte.</summary>
        Off = 0,

        /// <summary>Orange clignotant : démarrage, réseau, threads TCP / UDP / RUN.</summary>
        Starting = 1,

        /// <summary>Vert fixe : runtime prêt.</summary>
        Ready = 2,

        /// <summary>Rouge fixe : problème au démarrage.</summary>
        Fault = 3,

        /// <summary>Orange clignotant : parsing d'un programme.</summary>
        Parsing = 4,

        /// <summary>Vert fixe : programme lancé.</summary>
        Running = 5,

        /// <summary>Rouge fixe : erreur de parsing ou d'exécution.</summary>
        ProgramError = 6,

        /// <summary>Bleu fixe : programme en pause (debug). Prioritaire sur la couleur du script.</summary>
        Paused = 7,

        /// <summary>Flash blanc bref (pas à pas), puis retour automatique à Paused.</summary>
        Stepping = 8
    }
}
