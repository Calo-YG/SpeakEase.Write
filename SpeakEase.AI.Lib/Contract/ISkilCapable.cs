using SpeakEase.AI.Lib.Models;
using SpeakEase.AI.Lib.OpenAIModel;

namespace SpeakEase.AI.Lib.Contract
{
    public interface ISkilCapable
    {
        /// <summary>
        /// 技能列表
        /// </summary>
        IReadOnlyList<SkillDefinition> Skills { get; }

        /// <summary>
        /// 注册技能
        /// </summary>
        /// <param name="skill"></param>
        void RegiSkill(SkillDefinition skill);

        /// <summary>
        /// 构建Skills提示词
        /// </summary>
        /// <returns></returns>
        string BuildSkillPropmt();
    }
}
