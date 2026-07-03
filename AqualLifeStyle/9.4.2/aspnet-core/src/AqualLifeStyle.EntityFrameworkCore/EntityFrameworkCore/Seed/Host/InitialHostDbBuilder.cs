namespace AqualLifeStyle.EntityFrameworkCore.Seed.Host
{
    public class InitialHostDbBuilder
    {
        private readonly AqualLifeStyleDbContext _context;

        public InitialHostDbBuilder(AqualLifeStyleDbContext context)
        {
            _context = context;
        }

        public void Create()
        {
            new DefaultEditionCreator(_context).Create();
            new DefaultLanguagesCreator(_context).Create();
            new HostRoleAndUserCreator(_context).Create();
            new DefaultSettingsCreator(_context).Create();
            new HostDemoDataBuilder(_context).Create();

            _context.SaveChanges();
        }
    }
}
