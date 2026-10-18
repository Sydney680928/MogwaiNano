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
    /// États du runtime affichés par la LED intégrée (couleurs fixes, sans clignotement).
    /// </summary>
    public enum LedState
    {
        /// <summary>LED éteinte.</summary>
        Off = 0,

        /// <summary>Orange : initialisation de l'application.</summary>
        Initializing = 1,

        /// <summary>Vert : application prête, aucun programme en cours (idle).</summary>
        Idle = 2,

        /// <summary>Bleu : un programme est en cours d'exécution.</summary>
        Running = 3,

        /// <summary>Orange : programme en pause (debug). Prioritaire sur la couleur du script.</summary>
        Paused = 4,

        /// <summary>Rouge : erreur d'initialisation, de lancement ou d'exécution (halt compris).</summary>
        Error = 5
    }
}
